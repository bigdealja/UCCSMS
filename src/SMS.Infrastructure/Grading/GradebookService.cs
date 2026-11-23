using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Grading;

public record ComputedGrade(double CourseworkPct, double? EosaPct, double FinalPct, string Letter, double QualityPoints, bool Passed, string Reason);
public record PostResult(Guid BatchId, int Written, int Skipped);

/// <summary>The in-system gradebook: assessment components + entered marks are calculated here into
/// coursework%, end-of-semester%, final% and a letter grade (via the policy grade scheme) — no
/// external grade sheet. Posting writes the computed result onto each enrolment as an undoable batch.</summary>
public class GradebookService
{
    private readonly SmsDbContext _db;
    private readonly GradingService _grading;
    public GradebookService(SmsDbContext db, GradingService grading) { _db = db; _grading = grading; }

    public async Task<AssessmentComponent> AddComponentAsync(int courseId, int termId, string name, GradeCategory category, double weight, double maxMark, CancellationToken ct = default)
    {
        if (weight <= 0) throw new InvalidOperationException("Weight must be positive.");
        if (maxMark <= 0) throw new InvalidOperationException("Max mark must be positive.");
        var c = new AssessmentComponent { CourseId = courseId, TermId = termId, Name = name, Category = category, Weight = weight, MaxMark = maxMark };
        _db.AssessmentComponents.Add(c); await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task EnterMarkAsync(int componentId, int studentId, double mark, string by, string source = "Manual", CancellationToken ct = default)
    {
        var comp = await _db.AssessmentComponents.FirstOrDefaultAsync(c => c.Id == componentId, ct)
                   ?? throw new InvalidOperationException("Component not found.");
        if (mark < 0 || mark > comp.MaxMark) throw new InvalidOperationException($"Mark must be between 0 and {comp.MaxMark}.");
        var existing = await _db.ComponentMarks.FirstOrDefaultAsync(m => m.AssessmentComponentId == componentId && m.StudentId == studentId, ct);
        if (existing is null) _db.ComponentMarks.Add(new ComponentMark { AssessmentComponentId = componentId, StudentId = studentId, Mark = mark, EnteredBy = by, Source = source });
        else { existing.Mark = mark; existing.EnteredBy = by; existing.Source = source; }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<double> WeightTotalAsync(int courseId, int termId, CancellationToken ct = default)
        => await _db.AssessmentComponents.Where(c => c.CourseId == courseId && c.TermId == termId).SumAsync(c => c.Weight, ct);

    public async Task<ComputedGrade> ComputeAsync(int courseId, int termId, int studentId, CancellationToken ct = default)
    {
        var comps = await _db.AssessmentComponents.AsNoTracking().Where(c => c.CourseId == courseId && c.TermId == termId).ToListAsync(ct);
        var marks = (await _db.ComponentMarks.AsNoTracking()
            .Where(m => m.StudentId == studentId && comps.Select(c => c.Id).Contains(m.AssessmentComponentId)).ToListAsync(ct))
            .ToDictionary(m => m.AssessmentComponentId, m => m.Mark);

        double cwWeight = comps.Where(c => c.Category == GradeCategory.Coursework).Sum(c => c.Weight);
        double eosaWeight = comps.Where(c => c.Category == GradeCategory.EndOfSemester).Sum(c => c.Weight);
        double totalWeight = cwWeight + eosaWeight;

        double Contribution(IEnumerable<AssessmentComponent> cs) =>
            cs.Sum(c => (marks.TryGetValue(c.Id, out var mk) ? mk / c.MaxMark : 0) * c.Weight);

        double cwContribution = Contribution(comps.Where(c => c.Category == GradeCategory.Coursework));
        double eosaContribution = Contribution(comps.Where(c => c.Category == GradeCategory.EndOfSemester));

        double courseworkPct = cwWeight > 0 ? cwContribution / cwWeight * 100 : 0;
        double? eosaPct = eosaWeight > 0 ? eosaContribution / eosaWeight * 100 : (double?)null;
        double finalPct = totalWeight > 0 ? (cwContribution + eosaContribution) / totalWeight * 100 : 0;

        var prog = await _db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.ProgrammeId).FirstOrDefaultAsync(ct);
        var scheme = await _grading.GetSchemeForProgrammeAsync(prog, null, ct)
                     ?? throw new InvalidOperationException("No grade scheme for the student's programme.");
        var grade = GradingService.Grade(scheme, finalPct);
        var pass = GradingService.CheckPass(scheme, courseworkPct, eosaPct ?? scheme.EosaPassMark, finalPct);

        return new ComputedGrade(Math.Round(courseworkPct, 1), eosaPct is null ? null : Math.Round(eosaPct.Value, 1),
            Math.Round(finalPct, 1), grade.Letter, grade.QualityPoints, pass.Passed, pass.Reason);
    }

    /// <summary>Calculate and write the gradebook result onto every registered enrolment for the
    /// course/term, as one undoable batch (GradeSource = "Gradebook"). Withheld grades are skipped.</summary>
    public async Task<PostResult> PostAsync(int courseId, int termId, CancellationToken ct = default)
    {
        var batch = Guid.NewGuid();
        var enrolments = await _db.Enrollments.Where(e => e.CourseId == courseId && e.TermId == termId && e.Status == EnrollmentStatus.Registered).ToListAsync(ct);
        int written = 0, skipped = 0;
        foreach (var e in enrolments)
        {
            if (e.IsWithheld) { skipped++; continue; }
            var g = await ComputeAsync(courseId, termId, e.StudentId, ct);
            e.CourseworkMark = g.CourseworkPct;
            e.FinalExamMark = g.FinalPct;
            e.LetterGrade = g.Letter;
            e.GradeSource = "Gradebook";
            e.GradedUtc = DateTimeOffset.UtcNow;
            e.ImportBatchId = batch;
            written++;
        }
        await _db.SaveChangesAsync(ct);
        return new PostResult(batch, written, skipped);
    }
}
