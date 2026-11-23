using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMS.Application.Ai;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Admissions;

/// <summary>Produces an ADVISORY admission recommendation for an application: extracts/judges the
/// applicant against the UCC Admissions Policy categories and flags missing documents. Uses the
/// configured AI provider when enabled; otherwise falls back to deterministic rules. The output is
/// a suggestion only — a human still approves the decision through the second-person workflow.</summary>
public class AdmissionsAiService
{
    private readonly SmsDbContext _db;
    private readonly IAiAssistant _ai;
    public AdmissionsAiService(SmsDbContext db, IAiAssistant ai) { _db = db; _ai = ai; }

    const string SystemPrompt = """
You are an admissions assistant for the University of the Commonwealth Caribbean (UCC).
Recommend ONE admission category for the applicant, strictly per the UCC Admissions Policy:
- FullAcceptance: 5+ CXC/CSEC/GCE subjects incl. English and Mathematics at grades I–II / A–B.
- FullAcceptanceToDoCore: 5 subjects incl. English/Math at grade III (must take UCC foundation courses).
- ConditionalClass1: 4 subjects; not fully matriculated (must clear conditions in year 1).
- ConditionalClass2Mature: mature-entry route, lacks the basic academic qualification but has experience.
- MatureEntrant: 25+ years, 5+ years relevant experience, alternative qualifications.
- Provisional: graduate applicants at initial application with required docs pending.
You never make the final decision. Respond ONLY with compact JSON:
{"category":"<one of the above>","confidence":0.0-1.0,"rationale":"<short>","missingDocuments":["..."]}
""";

    public async Task<AdmissionRecommendation> RecommendAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await _db.Applications.Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");
        var programme = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == app.ProgrammeId, ct);

        var rec = (_ai.IsConfigured ? await TryAiAsync(app, programme, ct) : null)
                  ?? RuleBased(app, programme);
        rec.ApplicationId = applicationId;

        _db.AdmissionRecommendations.Add(rec);
        await _db.SaveChangesAsync(ct);
        return rec;
    }

    async Task<AdmissionRecommendation?> TryAiAsync(AdmissionApplication app, Programme? programme, CancellationToken ct)
    {
        var missing = app.Documents.Where(d => d.Required && !d.Received).Select(d => d.DocType).ToList();
        var context = JsonSerializer.Serialize(new
        {
            programme = programme?.Name,
            level = programme?.Level.ToString(),
            app.IsMatureEntrant, app.IsInternational,
            documents = app.Documents.Select(d => new { d.DocType, d.Received, d.Verified }),
            knownMissing = missing
        });
        try
        {
            var raw = await _ai.CompleteAsync(SystemPrompt, "Applicant context:\n" + context, ct);
            var json = ExtractJson(raw);
            if (json is null) return null;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cat = Enum.TryParse<AdmissionCategory>(root.GetProperty("category").GetString(), true, out var c) ? c : AdmissionCategory.None;
            return new AdmissionRecommendation
            {
                Source = "AI",
                RecommendedCategory = cat,
                Confidence = root.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetDouble() : 0.5,
                Rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() : null,
                MissingDocuments = root.TryGetProperty("missingDocuments", out var md) ? md.GetRawText() : JsonSerializer.Serialize(missing)
            };
        }
        catch { return null; }   // any AI/parse failure -> fall back to rules
    }

    static AdmissionRecommendation RuleBased(AdmissionApplication app, Programme? programme)
    {
        var missing = app.Documents.Where(d => d.Required && !d.Received).Select(d => d.DocType).ToList();
        var (category, rationale, confidence) = (programme?.Level, app.IsMatureEntrant) switch
        {
            (ProgrammeLevel.Masters, _) => (AdmissionCategory.Provisional, "Graduate applicant — provisional pending document review.", 0.5),
            (_, true) => (AdmissionCategory.ConditionalClass2Mature, "Flagged as mature entrant — route via mature-entry conditions.", 0.5),
            _ => (AdmissionCategory.None, "Insufficient structured data to auto-recommend; manual review required.", 0.3)
        };
        if (missing.Count > 0 && category != AdmissionCategory.None)
        { rationale += $" Missing documents: {string.Join(", ", missing)}."; confidence = Math.Min(confidence, 0.4); }

        return new AdmissionRecommendation
        {
            Source = "Rules", RecommendedCategory = category, Confidence = confidence,
            Rationale = rationale, MissingDocuments = JsonSerializer.Serialize(missing)
        };
    }

    static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        return a >= 0 && b > a ? raw[a..(b + 1)] : null;
    }
}
