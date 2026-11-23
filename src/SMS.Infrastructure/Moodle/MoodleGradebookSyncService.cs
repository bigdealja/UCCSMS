using Microsoft.EntityFrameworkCore;
using SMS.Application.Moodle;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Moodle;

public record MoodleItemRef(string ItemId, string ItemName);
public record GradebookSyncResult(int MarksWritten, int Unmatched, int MappedComponents);

/// <summary>Syncs Moodle activity marks into the in-system gradebook: lists a linked Moodle course's
/// grade items (so each can be mapped to an AssessmentComponent), and pulls each student's raw mark
/// for mapped components into ComponentMarks (scaled to the component's max mark).</summary>
public class MoodleGradebookSyncService
{
    private readonly SmsDbContext _db;
    private readonly IMoodleClient _moodle;
    public MoodleGradebookSyncService(SmsDbContext db, IMoodleClient moodle) { _db = db; _moodle = moodle; }

    async Task<long> LinkedCourseAsync(int courseId, CancellationToken ct)
    {
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct)
                     ?? throw new InvalidOperationException("Course not found.");
        return course.MoodleCourseId ?? throw new InvalidOperationException("Course is not linked to Moodle — provision it first.");
    }

    /// <summary>Distinct grade items on the linked Moodle course (id + name), for mapping.</summary>
    public async Task<IReadOnlyList<MoodleItemRef>> ListItemsAsync(int courseId, CancellationToken ct = default)
    {
        var moodleCourseId = await LinkedCourseAsync(courseId, ct);
        var users = await _moodle.GetGradeItemsAsync(moodleCourseId, ct);
        return users.SelectMany(u => u.Items).GroupBy(i => i.ItemId)
            .Select(g => new MoodleItemRef(g.Key, g.First().ItemName)).ToList();
    }

    /// <summary>Pull marks for components that are mapped to a Moodle item into ComponentMarks.</summary>
    public async Task<GradebookSyncResult> SyncAsync(int courseId, int termId, CancellationToken ct = default)
    {
        var moodleCourseId = await LinkedCourseAsync(courseId, ct);
        var components = await _db.AssessmentComponents
            .Where(c => c.CourseId == courseId && c.TermId == termId && c.MoodleItemId != null).ToListAsync(ct);
        if (components.Count == 0) return new GradebookSyncResult(0, 0, 0);

        var byItem = components.ToDictionary(c => c.MoodleItemId!, c => c);
        var users = await _moodle.GetGradeItemsAsync(moodleCourseId, ct);
        var students = await _db.Students.AsNoTracking().ToListAsync(ct);

        int written = 0, unmatched = 0;
        foreach (var u in users)
        {
            var student = students.FirstOrDefault(s => s.StudentNumber == u.UserIdNumber || (u.Email != null && s.Email == u.Email));
            if (student is null) { unmatched++; continue; }
            foreach (var item in u.Items)
            {
                if (!byItem.TryGetValue(item.ItemId, out var comp) || item.Raw is null) continue;
                double scaled = item.Max is > 0 ? item.Raw.Value / item.Max.Value * comp.MaxMark : item.Raw.Value;
                var existing = await _db.ComponentMarks.FirstOrDefaultAsync(m => m.AssessmentComponentId == comp.Id && m.StudentId == student.Id, ct);
                if (existing is null) _db.ComponentMarks.Add(new ComponentMark { AssessmentComponentId = comp.Id, StudentId = student.Id, Mark = Math.Round(scaled, 2), EnteredBy = "moodle-sync", Source = "Moodle" });
                else { existing.Mark = Math.Round(scaled, 2); existing.Source = "Moodle"; existing.EnteredBy = "moodle-sync"; }
                written++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return new GradebookSyncResult(written, unmatched, components.Count);
    }
}
