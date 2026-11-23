using Microsoft.EntityFrameworkCore;
using SMS.Application.Moodle;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Moodle;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class MoodleGradebookSyncTests
{
    class ItemsFakeClient : IMoodleClient
    {
        public List<MoodleUserItems> Items = new();
        public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<MoodleUser> EnsureUserAsync(string u, string e, string f, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MoodleCourseRef> EnsureCourseAsync(string s, string f, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnrolUserAsync(long u, long c, int r, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<MoodleGrade>> GetCourseGradesAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<MoodleGrade>)new List<MoodleGrade>());
        public Task<IReadOnlyList<MoodleUserItems>> GetGradeItemsAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<MoodleUserItems>)Items);
    }

    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "P" };
        db.Courses.Add(new Course { Id = 10, Code = "C", Title = "C", Programme = prog, MoodleCourseId = 555 });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Students.Add(new Student { Id = 1, StudentNumber = "S0001", FirstName = "Ada", LastName = "L", Email = "ada@x.edu", Programme = prog });
        db.AssessmentComponents.Add(new AssessmentComponent { Id = 100, CourseId = 10, TermId = 7, Name = "Quiz", Category = GradeCategory.Coursework, Weight = 50, MaxMark = 20, MoodleItemId = "901" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Syncs_mapped_moodle_item_marks_into_components_scaled_to_max()
    {
        using var db = NewDb();
        var fake = new ItemsFakeClient {
            Items = { new MoodleUserItems("S0001", "ada@x.edu", new[] {
                new MoodleGradeItem("901", "Quiz", Raw: 45, Max: 50),      // mapped -> scale 45/50 to /20 = 18
                new MoodleGradeItem("902", "Unmapped", Raw: 10, Max: 10) }) }
        };
        var svc = new MoodleGradebookSyncService(db, fake);
        var result = await svc.SyncAsync(10, 7);

        Assert.Equal(1, result.MarksWritten);                 // only the mapped item
        var mark = await db.ComponentMarks.FirstAsync(m => m.AssessmentComponentId == 100 && m.StudentId == 1);
        Assert.Equal(18, mark.Mark, 1);                       // scaled to the component's max
        Assert.Equal("Moodle", mark.Source);
    }

    [Fact]
    public async Task List_items_returns_distinct_items_from_the_linked_course()
    {
        using var db = NewDb();
        var fake = new ItemsFakeClient {
            Items = { new MoodleUserItems("S0001", "ada@x.edu", new[] {
                new MoodleGradeItem("901", "Quiz", 10, 20), new MoodleGradeItem("902", "Final", 50, 100) }) }
        };
        var items = await new MoodleGradebookSyncService(db, fake).ListItemsAsync(10);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.ItemId == "901" && i.ItemName == "Quiz");
    }

    [Fact]
    public async Task Sync_fails_clearly_when_course_not_linked_to_moodle()
    {
        using var db = NewDb();
        var c = await db.Courses.FindAsync(10); c!.MoodleCourseId = null; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MoodleGradebookSyncService(db, new ItemsFakeClient()).SyncAsync(10, 7));
    }
}
