using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Advisement;

public record CourseProgress(int CourseId, string Code, string Title, int Credits, string? Grade, bool Passed, bool Attempted);
public record StudentProgress(int StudentId, string Programme, string Level, int CreditsRequired, int CreditsEarned,
    double Gpa, IReadOnlyList<CourseProgress> Completed, IReadOnlyList<CourseProgress> Outstanding, IReadOnlyList<CourseProgress> Failed);
public record GraduationVerdict(bool Eligible, double Gpa, double MinGpa, int CreditsEarned, int CreditsRequired,
    int OutstandingCount, IReadOnlyList<string> Reasons);

/// <summary>Academic advisement & graduation check: measures a student against their programme's
/// curriculum and credit requirement using the policy grade scheme, and gives a graduation verdict.</summary>
public class AdvisementService
{
    private readonly SmsDbContext _db;
    private readonly GradingService _grading;
    public AdvisementService(SmsDbContext db, GradingService grading) { _db = db; _grading = grading; }

    static double MinGpaFor(ProgrammeLevel level) => level == ProgrammeLevel.Masters ? 3.0 : 2.0;

    public async Task<StudentProgress> GetProgressAsync(int studentId, CancellationToken ct = default)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct)
                      ?? throw new InvalidOperationException("Student not found.");
        var programme = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == student.ProgrammeId, ct)
                        ?? throw new InvalidOperationException("Programme not found.");
        var scheme = await _grading.GetSchemeForProgrammeAsync(programme.Id, null, ct);
        var passByLetter = scheme?.Bands.ToDictionary(b => b.Letter, b => b.IsPass, StringComparer.OrdinalIgnoreCase)
                           ?? new Dictionary<string, bool>();

        // Curriculum = all courses in the programme
        var curriculum = await _db.Courses.AsNoTracking().Where(c => c.ProgrammeId == programme.Id).ToListAsync(ct);
        var enrolments = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId).ToListAsync(ct);
        var byCourse = enrolments.GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.GradedUtc ?? e.CreatedUtc).First());

        bool Passed(Enrollment e) =>
            e.LetterGrade is not null && passByLetter.TryGetValue(e.LetterGrade, out var p) && p;

        var completed = new List<CourseProgress>(); var outstanding = new List<CourseProgress>(); var failed = new List<CourseProgress>();
        foreach (var c in curriculum)
        {
            byCourse.TryGetValue(c.Id, out var e);
            bool attempted = e is not null && e.LetterGrade is not null;
            bool passed = e is not null && Passed(e);
            var cp = new CourseProgress(c.Id, c.Code, c.Title, c.Credits, e?.LetterGrade, passed, attempted);
            if (passed) completed.Add(cp);
            else { outstanding.Add(cp); if (attempted) failed.Add(cp); }
        }

        int creditsEarned = completed.Sum(c => c.Credits);
        double gpa = await _grading.ComputeGpaAsync(studentId, ct);
        return new StudentProgress(studentId, programme.Name, programme.Level.ToString(), programme.CreditsRequired,
            creditsEarned, gpa, completed, outstanding, failed);
    }

    public async Task<GraduationVerdict> GraduationCheckAsync(int studentId, CancellationToken ct = default)
    {
        var p = await GetProgressAsync(studentId, ct);
        var level = Enum.Parse<ProgrammeLevel>(p.Level);
        double minGpa = MinGpaFor(level);
        var reasons = new List<string>();
        if (p.CreditsRequired > 0 && p.CreditsEarned < p.CreditsRequired)
            reasons.Add($"{p.CreditsRequired - p.CreditsEarned} credits short ({p.CreditsEarned}/{p.CreditsRequired}).");
        if (p.Outstanding.Count > 0)
            reasons.Add($"{p.Outstanding.Count} outstanding course(s): {string.Join(", ", p.Outstanding.Take(5).Select(o => o.Code))}{(p.Outstanding.Count > 5 ? "…" : "")}.");
        if (p.Gpa < minGpa)
            reasons.Add($"GPA {p.Gpa:0.00} is below the {minGpa:0.00} minimum.");
        return new GraduationVerdict(reasons.Count == 0, p.Gpa, minGpa, p.CreditsEarned, p.CreditsRequired, p.Outstanding.Count, reasons);
    }
}
