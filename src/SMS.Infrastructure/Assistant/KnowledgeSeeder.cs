using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Assistant;

/// <summary>Seeds a starter knowledge base from the UCC policies already encoded in the system.
/// Editable afterwards as the policy library.</summary>
public static class KnowledgeSeeder
{
    public static async Task EnsureSeededAsync(SmsDbContext db, CancellationToken ct = default)
    {
        if (await db.KnowledgeArticles.IgnoreQueryFilters().AnyAsync(ct)) return;
        (string, string, string, string)[] arts =
        {
            ("Passing a course", "Grading",
             "To pass a course you must score at least 50% in total coursework AND at least 50% in the End-of-Semester Assessment, AND reach the overall pass mark (50% undergraduate/Occupational, 60% Masters).",
             "pass passing grade coursework exam fail 50"),
            ("How GPA is calculated", "Grading",
             "GPA = sum(quality points x course credits) / sum(credits), using the grade scheme for your programme level. Grades that do not count (e.g. Incomplete 'I') are excluded.",
             "gpa grade point average quality points credits"),
            ("Re-sit (RE) rule", "Grading",
             "If you pass coursework (70-100%) but score 45-49% in the final exam, you may re-sit at the next sitting. A re-sit mark is capped at a pass (50%) and shows as grade 'RE'.",
             "resit re retake final exam cap"),
            ("Incomplete (I) grade", "Grading",
             "An 'I' grade may be given for good cause (with documents). It must be cleared in the designated period or it reverts to 'F'. A student may hold at most four 'I' grades in a programme. 'I' does not count in GPA.",
             "incomplete i grade deferred alternative exam"),
            ("Admission categories", "Admissions",
             "Undergraduate categories: Full Acceptance (5 subjects incl. English & Math at I-II/A-B), Full-to-do-Core (grade III, must take foundation courses), Conditional Class 1 (4 subjects), Conditional Class 2 / Mature Entrant. Graduate: Full, Provisional, Conditional.",
             "admission acceptance conditional mature entry categories"),
            ("Deferring your start", "Admissions",
             "An admitted undergraduate student may request a one-time, one-year deferral of their start date using the Placement Offer Response Form, submitted at least two weeks before the semester starts.",
             "defer deferral placement offer start date"),
            ("Exam special arrangements", "Exams",
             "Approved special arrangements (extra time, separate room, reader, scribe, assistive technology) are applied to your exams. Extra time extends your exam window and is accounted for in clash checking.",
             "special arrangement extra time reader scribe disability exam"),
            ("Case turnaround", "Cases",
             "Each case type has a turnaround in working days and a due date. Sensitive types (grade amendments, appeals, irregularities) require approval by a second person who is not the raiser or assignee.",
             "case query appeal turnaround due approval"),
        };
        foreach (var (title, cat, body, tags) in arts)
            db.KnowledgeArticles.Add(new KnowledgeArticle { Title = title, Category = cat, Body = body, Tags = tags });
        await db.SaveChangesAsync(ct);
    }
}
