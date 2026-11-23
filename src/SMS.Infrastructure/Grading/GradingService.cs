using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Grading;

public record GradeResult(string Letter, double QualityPoints, bool IsPass, bool CountsInGpa);
public record PassCheck(bool Passed, string Reason);

/// <summary>Applies the UCC Examination Policy grading rules from configurable GradeSchemes.
/// Picks the scheme by the programme's level + effective date; never hard-codes the scale.</summary>
public class GradingService
{
    private readonly SmsDbContext _db;
    public GradingService(SmsDbContext db) => _db = db;

    public async Task<GradeScheme?> GetSchemeForProgrammeAsync(int programmeId, DateOnly? on = null, CancellationToken ct = default)
    {
        var prog = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == programmeId, ct);
        if (prog is null) return null;
        return await GetSchemeForLevelAsync(prog.Level, on, ct);
    }

    public async Task<GradeScheme?> GetSchemeForLevelAsync(ProgrammeLevel level, DateOnly? on = null, CancellationToken ct = default)
    {
        var asOf = on ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.GradeSchemes.Include(s => s.Bands)
            .Where(s => s.Level == level && s.IsActive && s.EffectiveFrom <= asOf)
            .OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync(ct);
    }

    /// <summary>Map a final percentage to a letter grade + quality points for a scheme.</summary>
    public static GradeResult Grade(GradeScheme scheme, double finalPercent)
    {
        var band = scheme.Bands
            .Where(b => !b.IsSpecial && b.MinPercent is not null && b.MaxPercent is not null)
            .FirstOrDefault(b => finalPercent >= b.MinPercent && finalPercent <= b.MaxPercent);
        if (band is null)
        {
            // Below all pass bands -> the fail band (F/U), else lowest band.
            band = scheme.Bands.Where(b => !b.IsSpecial).OrderBy(b => b.MinPercent ?? -1).FirstOrDefault()
                   ?? throw new InvalidOperationException("Scheme has no bands.");
        }
        return new GradeResult(band.Letter, band.QualityPoints, band.IsPass, band.CountsInGpa);
    }

    /// <summary>Policy pass rules: coursework >= 50 AND EOSA >= 50 AND combined >= overall threshold.</summary>
    public static PassCheck CheckPass(GradeScheme s, double courseworkPct, double eosaPct, double finalPct)
    {
        if (courseworkPct < s.CourseworkPassMark)
            return new PassCheck(false, $"Coursework {courseworkPct:0.#}% is below the {s.CourseworkPassMark:0.#}% minimum.");
        if (eosaPct < s.EosaPassMark)
            return new PassCheck(false, $"End-of-Semester Assessment {eosaPct:0.#}% is below the {s.EosaPassMark:0.#}% minimum.");
        if (finalPct < s.OverallPassMark)
            return new PassCheck(false, $"Combined {finalPct:0.#}% is below the {s.OverallPassMark:0.#}% pass mark.");
        return new PassCheck(true, "Meets all pass requirements.");
    }

    /// <summary>GPA = sum(qualityPoints * credits) / sum(credits) over GPA-counting graded enrolments.</summary>
    public async Task<double> ComputeGpaAsync(int studentId, CancellationToken ct = default)
    {
        var prog = await _db.Students.AsNoTracking().Where(s => s.Id == studentId)
            .Select(s => s.ProgrammeId).FirstOrDefaultAsync(ct);
        var scheme = await GetSchemeForProgrammeAsync(prog, null, ct);
        if (scheme is null) return 0;

        var rows = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.FinalExamMark != null)
            .Select(e => new { e.FinalExamMark, e.Course.Credits }).ToListAsync(ct);

        double points = 0; int credits = 0;
        foreach (var r in rows)
        {
            var g = Grade(scheme, r.FinalExamMark!.Value);
            if (!g.CountsInGpa) continue;
            points += g.QualityPoints * r.Credits;
            credits += r.Credits;
        }
        return credits == 0 ? 0 : Math.Round(points / credits, 2);
    }
}
