using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Grading;

/// <summary>Seeds the four UCC grading scales from the Examination Policy (Section XII).
/// Idempotent. These are starting values — editable afterwards as versioned config.</summary>
public static class GradeSchemeSeeder
{
    public static async Task EnsureSeededAsync(SmsDbContext db, CancellationToken ct = default)
    {
        if (await db.GradeSchemes.IgnoreQueryFilters().AnyAsync(ct)) return;
        var eff = new DateOnly(2026, 8, 1);

        // Letter, min, max, QP, isPass
        void Scheme(string name, ProgrammeLevel level, double overall, (string, double?, double?, double, bool)[] bands,
                    (string letter, bool countsGpa, bool isPass)[] specials)
        {
            var s = new GradeScheme { Name = name, Level = level, EffectiveFrom = eff, OverallPassMark = overall };
            int order = 0;
            foreach (var (L, mn, mx, qp, pass) in bands)
                s.Bands.Add(new GradeBand { Letter = L, MinPercent = mn, MaxPercent = mx, QualityPoints = qp, IsPass = pass, SortOrder = order++ });
            foreach (var (letter, countsGpa, isPass) in specials)
                s.Bands.Add(new GradeBand { Letter = letter, IsSpecial = true, CountsInGpa = countsGpa, IsPass = isPass, QualityPoints = 0, SortOrder = order++ });
            db.GradeSchemes.Add(s);
        }

        var specials = new (string, bool, bool)[] {
            ("I", false, false), ("RE", true, true), ("DNS", true, false),
            ("W", false, false), ("WF", true, false), ("DEF", false, false),
            ("MED", false, false), ("AM", true, true), ("EC", false, false)
        };

        // Bachelors (policy table)
        Scheme("UCC Bachelors", ProgrammeLevel.Bachelors, 50, new (string, double?, double?, double, bool)[] {
            ("A", 90, 100, 4.00, true), ("A-", 80, 89.99, 3.83, true), ("B+", 75, 79.99, 3.58, true),
            ("B", 65, 74.99, 3.25, true), ("B-", 60, 64.99, 2.83, true), ("C+", 55, 59.99, 2.50, true),
            ("C", 50, 54.99, 2.16, true), ("F", 0, 49.99, 0.00, false)
        }, specials);

        // Masters (policy table; overall pass 60)
        Scheme("UCC Masters", ProgrammeLevel.Masters, 60, new (string, double?, double?, double, bool)[] {
            ("A", 90, 100, 3.84, true), ("A-", 80, 89.99, 3.59, true), ("B+", 70, 79.99, 3.25, true),
            ("B", 60, 69.99, 3.00, true), ("F", 0, 59.99, 0.00, false)
        }, specials);

        // Occupational Associate Degree (policy table)
        Scheme("UCC Occupational Associate", ProgrammeLevel.OccupationalAssociate, 50, new (string, double?, double?, double, bool)[] {
            ("A", 90, 100, 4.00, true), ("A-", 80, 89.99, 3.67, true), ("B+", 75, 79.99, 3.33, true),
            ("B", 70, 74.99, 3.00, true), ("B-", 65, 69.99, 2.67, true), ("C+", 60, 64.99, 2.33, true),
            ("C", 55, 59.99, 2.00, true), ("C-", 50, 54.99, 1.67, true), ("D+", 45, 49.99, 1.33, false),
            ("D", 40, 44.99, 1.00, false), ("U", 0, 39.99, 0.00, false)
        }, specials);

        // Occupational Diploma / Certificate (policy table)
        Scheme("UCC Occupational Diploma", ProgrammeLevel.OccupationalDiploma, 50, new (string, double?, double?, double, bool)[] {
            ("A", 89.5, 100, 4.00, true), ("A-", 79.5, 89.49, 3.67, true), ("B+", 75.5, 79.49, 3.50, true),
            ("B", 64.5, 75.49, 3.00, true), ("B-", 59.5, 64.49, 2.67, true), ("C+", 54.5, 59.49, 2.33, true),
            ("C", 49.5, 54.49, 2.00, true), ("F", 0, 49.49, 0.00, false)
        }, specials);

        await db.SaveChangesAsync(ct);
    }
}
