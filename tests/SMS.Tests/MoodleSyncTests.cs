using Microsoft.EntityFrameworkCore;
using SMS.Application.Moodle;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Moodle;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

/// <summary>In-memory Moodle that records calls, so we can test sync mapping without a live server.</summary>
class FakeMoodleClient : IMoodleClient
{
    public long NextUserId = 1000, NextCourseId = 500;
    public List<(long user, long course)> Enrolments = new();
    public List<MoodleGrade> GradesToReturn = new();

    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<MoodleUser> EnsureUserAsync(string username, string email, string fn, string ln, CancellationToken ct = default)
        => Task.FromResult(new MoodleUser(NextUserId++, username, email, fn, ln));
    public Task<MoodleCourseRef> EnsureCourseAsync(string shortName, string fullName, CancellationToken ct = default)
        => Task.FromResult(new MoodleCourseRef(NextCourseId++, shortName, fullName));
    public Task EnrolUserAsync(long userId, long courseId, int roleId, CancellationToken ct = default)
    { Enrolments.Add((userId, courseId)); return Task.CompletedTask; }
    public Task<IReadOnlyList<MoodleGrade>> GetCourseGradesAsync(long courseId, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<MoodleGrade>)GradesToReturn);
}

public class MoodleSyncTests
{
    static SmsDbContext NewDb(out Programme prog, out Course course, out Term term)
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        prog = new Programme { Id = 1, Code = "P", Name = "Prog" };
        course = new Course { Id = 10, Code = "CS101", Title = "Intro", Programme = prog };
        term = new Term { Id = 7, Name = "Fall", Year = 2026, Type = TermType.Semester };
        db.AddRange(course, term);
        var s1 = new Student { Id = 1, StudentNumber = "S0001", FirstName = "Ada", LastName = "L", Email = "ada@x.edu", Programme = prog };
        var s2 = new Student { Id = 2, StudentNumber = "S0002", FirstName = "Al", LastName = "T", Email = "al@x.edu", Programme = prog };
        db.Students.AddRange(s1, s2);
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered },
            new Enrollment { StudentId = 2, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Provision_links_course_and_enrols_all_registered_students()
    {
        using var db = NewDb(out _, out _, out _);
        var fake = new FakeMoodleClient();
        var sync = new MoodleSyncService(db, fake);

        var result = await sync.ProvisionCourseAsync(10, 7, roleId: 5);

        Assert.Equal(2, result.UsersEnrolled);
        Assert.Equal(2, fake.Enrolments.Count);
        Assert.NotNull((await db.Courses.FindAsync(10))!.MoodleCourseId);
        Assert.All(await db.Students.ToListAsync(), s => Assert.NotNull(s.MoodleUserId));
    }

    [Fact]
    public async Task PullGrades_writes_moodle_grades_as_undoable_batch_and_counts_unmatched()
    {
        using var db = NewDb(out _, out var course, out _);
        course.MoodleCourseId = 555; await db.SaveChangesAsync();
        var fake = new FakeMoodleClient
        {
            GradesToReturn =
            {
                new MoodleGrade("S0001", "ada@x.edu", "A", 92.0),
                new MoodleGrade("S9999", "ghost@x.edu", "F", 10.0),   // not in SMS
            }
        };
        var sync = new MoodleSyncService(db, fake);

        var result = await sync.PullGradesAsync(10, 7);

        Assert.Equal(1, result.GradesWritten);
        Assert.Equal(1, result.Unmatched);
        var ada = await db.Enrollments.FirstAsync(e => e.StudentId == 1);
        Assert.Equal("A", ada.LetterGrade);
        Assert.Equal("Moodle", ada.GradeSource);
        Assert.Equal(result.BatchId, ada.ImportBatchId);   // same undo mechanism as file import
    }
}
