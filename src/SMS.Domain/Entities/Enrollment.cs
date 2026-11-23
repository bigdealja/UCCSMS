using SMS.Domain.Common;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities;

/// <summary>A student's registration in a course for a term, plus the grade.
/// Replaces the legacy sms_GradeTool_I_StudentCourses / I_StuCouEndFinals stored procs,
/// but as a first-class, auditable, undoable record (every import got a batch GUID in the old tool —
/// here ImportBatchId makes rollback trivial).</summary>
public class Enrollment : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = default!;

    public int CourseId { get; set; }
    public Course Course { get; set; } = default!;

    public int TermId { get; set; }
    public Term Term { get; set; } = default!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Registered;

    // Grading
    public string? LetterGrade { get; set; }
    public double? CourseworkMark { get; set; }
    public double? FinalExamMark { get; set; }

    // Provenance / undo for bulk grade imports and Moodle sync
    public Guid? ImportBatchId { get; set; }
    public string? GradeSource { get; set; }   // "Manual" | "Import" | "Moodle" | "Exam" | "GradeChange"

    // Grade governance
    public bool IsWithheld { get; set; }         // e.g. pending an integrity case
    public int? WithheldCaseId { get; set; }
    public DateTimeOffset? GradedUtc { get; set; }
}
