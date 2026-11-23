using System.Text;
using Microsoft.EntityFrameworkCore;
using SMS.Application.Ai;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Finance;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Assistant;

public record AssistantAnswer(string Answer, IReadOnlyList<string> Sources, bool UsedAi);

/// <summary>Answers staff/student questions GROUNDED in the knowledge base and the asker's own
/// records. The AI (when enabled) must answer only from supplied context; with AI off, a
/// deterministic answer is composed from the same context. Every query is logged.</summary>
public class AssistantService
{
    private readonly SmsDbContext _db;
    private readonly IAiAssistant _ai;
    private readonly GradingService _grading;
    private readonly FinanceService _finance;
    public AssistantService(SmsDbContext db, IAiAssistant ai, GradingService grading, FinanceService finance)
    { _db = db; _ai = ai; _grading = grading; _finance = finance; }

    const string System = """
You are the UCC student & staff assistant. Answer ONLY using the CONTEXT provided (policy articles
and, if present, the person's own record facts). If the answer is not in the context, say you don't
have that information and suggest contacting the relevant office. Be concise. Never invent policy,
grades, balances or decisions.
""";

    public async Task<AssistantAnswer> AskAsync(string question, int? studentId, string? askedBy, CancellationToken ct = default)
    {
        var articles = await RetrieveAsync(question, ct);
        var facts = studentId is int sid ? await GroundedFactsAsync(sid, ct) : null;

        var context = new StringBuilder();
        context.AppendLine("POLICY ARTICLES:");
        foreach (var a in articles) context.AppendLine($"- {a.Title}: {a.Body}");
        if (facts is not null) { context.AppendLine("\nRECORD FACTS (the asker's own):"); context.AppendLine(facts); }

        string answer; bool usedAi = false;
        if (_ai.IsConfigured)
        {
            var raw = await SafeCompleteAsync(System, $"CONTEXT:\n{context}\n\nQUESTION: {question}", ct);
            if (!string.IsNullOrWhiteSpace(raw)) { answer = raw.Trim(); usedAi = true; }
            else answer = Compose(articles, facts);
        }
        else answer = Compose(articles, facts);

        _db.AssistantQueryLogs.Add(new AssistantQueryLog { AskedBy = askedBy, StudentId = studentId,
            Question = question, Answer = answer.Length > 2000 ? answer[..2000] : answer, UsedAi = usedAi });
        await _db.SaveChangesAsync(ct);

        return new AssistantAnswer(answer, articles.Select(a => a.Title).ToList(), usedAi);
    }

    async Task<List<KnowledgeArticle>> RetrieveAsync(string question, CancellationToken ct)
    {
        var terms = question.ToLowerInvariant().Split(new[] { ' ', ',', '.', '?', '!', '\'', '"' },
            StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).Distinct().ToArray();
        var all = await _db.KnowledgeArticles.AsNoTracking().Where(a => a.IsPublished).ToListAsync(ct);
        return all.Select(a => new { a, score = Score(a, terms) })
                  .Where(x => x.score > 0).OrderByDescending(x => x.score).Take(3).Select(x => x.a).ToList();
    }

    static int Score(KnowledgeArticle a, string[] terms)
    {
        var hay = ((a.Title + " " + (a.Tags ?? "") + " " + a.Body)).ToLowerInvariant();
        return terms.Count(t => hay.Contains(t));
    }

    async Task<string> GroundedFactsAsync(int studentId, CancellationToken ct)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return "No student record found.";
        var gpa = await _grading.ComputeGpaAsync(studentId, ct);
        var balance = await _finance.GetBalanceAsync(studentId, ct);
        var activeTerm = await _db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        int upcoming = 0;
        if (activeTerm is int term)
            upcoming = await _db.Exams.AsNoTracking().Where(e => e.TermId == term && e.ScheduledStart != null
                && _db.Enrollments.Any(en => en.StudentId == studentId && en.CourseId == e.CourseId && en.TermId == term
                    && en.Status == EnrollmentStatus.Registered)).CountAsync(ct);
        int openCases = await _db.Cases.CountAsync(c => c.StudentId == studentId && c.ClosedUtc == null, ct);
        return $"Name: {student.FirstName} {student.LastName}; Status: {student.Status}; GPA: {gpa}; " +
               $"Account balance: {balance}; Upcoming exams this term: {upcoming}; Open cases: {openCases}.";
    }

    async Task<string> SafeCompleteAsync(string sys, string user, CancellationToken ct)
    { try { return await _ai.CompleteAsync(sys, user, ct); } catch { return string.Empty; } }

    static string Compose(List<KnowledgeArticle> articles, string? facts)
    {
        var sb = new StringBuilder();
        if (articles.Count == 0 && facts is null)
            return "I don't have information on that. Please contact the relevant office (Registry, Examinations or Student Finance).";
        if (facts is not null) sb.AppendLine(facts).AppendLine();
        foreach (var a in articles) sb.AppendLine($"{a.Title}: {a.Body}");
        sb.AppendLine("\n(AI is disabled; this answer is assembled directly from the policy library and your records.)");
        return sb.ToString().Trim();
    }
}
