using Microsoft.EntityFrameworkCore;
using SMS.Application.Moodle;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Moodle;

/// <summary>Keeps Moodle in step with the SMS (the system of record): provisions courses +
/// enrolments outward, and pulls grades back in as an undoable batch (reusing the Phase 1
/// ImportBatchId mechanism, so a Moodle grade sync can be rolled back exactly like a file import).</summary>
public class MoodleSyncService
{
    private readonly SmsDbContext _db;
    private readonly IMoodleClient _moodle;
    public MoodleSyncService(SmsDbContext db, IMoodleClient moodle) { _db = db; _moodle = moodle; }

    /// <summary>Ensure the Moodle course exists and every registered student is a Moodle user and enrolled.</summary>
    public async Task<ProvisionResult> ProvisionCourseAsync(int courseId, int termId, int roleId, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct)
                     ?? throw new InvalidOperationException($"Course {courseId} not found.");
        var term = await _db.Terms.FirstOrDefaultAsync(t => t.Id == termId, ct)
                   ?? throw new InvalidOperationException($"Term {termId} not found.");

        // 1) Course
        if (course.MoodleCourseId is null)
        {
            var shortName = $"{course.Code}-{term.Name}{term.Year}";
            var @ref = await _moodle.EnsureCourseAsync(shortName, $"{course.Title} ({term.Name} {term.Year})", ct);
            course.MoodleCourseId = @ref.Id;
        }

        // 2) Students registered for this course+term
        var students = await _db.Enrollments
            .Where(e => e.CourseId == courseId && e.TermId == termId && e.Status == EnrollmentStatus.Registered)
            .Select(e => e.Student).Distinct().ToListAsync(ct);

        int enrolled = 0, alreadyLinked = 0;
        foreach (var s in students)
        {
            if (s.MoodleUserId is null)
            {
                var mu = await _moodle.EnsureUserAsync(s.StudentNumber, s.Email ?? $"{s.StudentNumber}@example.edu",
                                                       s.FirstName, s.LastName, ct);
                s.MoodleUserId = mu.Id;
            }
            else alreadyLinked++;

            await _moodle.EnrolUserAsync(s.MoodleUserId!.Value, course.MoodleCourseId!.Value, roleId, ct);
            enrolled++;
        }

        await _db.SaveChangesAsync(ct);
        return new ProvisionResult(course.MoodleCourseId!.Value, enrolled, alreadyLinked);
    }

    /// <summary>Pull final grades from Moodle into SMS enrolments as one undoable batch.</summary>
    public async Task<GradePullResult> PullGradesAsync(int courseId, int termId, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct)
                     ?? throw new InvalidOperationException($"Course {courseId} not found.");
        if (course.MoodleCourseId is null)
            throw new InvalidOperationException("Course is not linked to Moodle yet — provision it first.");

        var grades = await _moodle.GetCourseGradesAsync(course.MoodleCourseId.Value, ct);
        var batchId = Guid.NewGuid();
        int written = 0, unmatched = 0;

        var enrollments = await _db.Enrollments.Include(e => e.Student)
            .Where(e => e.CourseId == courseId && e.TermId == termId).ToListAsync(ct);

        foreach (var g in grades)
        {
            var e = enrollments.FirstOrDefault(x =>
                x.Student.StudentNumber == g.UserIdNumber ||
                (g.Email != null && x.Student.Email == g.Email));
            if (e is null) { unmatched++; continue; }

            e.LetterGrade = g.LetterGrade;
            e.FinalExamMark = g.FinalMark;
            e.GradeSource = "Moodle";
            e.GradedUtc = DateTimeOffset.UtcNow;
            e.ImportBatchId = batchId;
            written++;
        }

        await _db.SaveChangesAsync(ct);
        return new GradePullResult(batchId, written, unmatched);
    }
}
