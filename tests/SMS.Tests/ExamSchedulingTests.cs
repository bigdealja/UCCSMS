using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class ExamSchedulingTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "Prog" };
        var campus = new Campus { Id = 1, Code = "MAIN", Name = "Main" };
        db.Campuses.Add(campus);
        db.Courses.AddRange(
            new Course { Id = 10, Code = "C1", Title = "Course 1", Programme = prog },
            new Course { Id = 11, Code = "C2", Title = "Course 2", Programme = prog });
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Rooms.Add(new Room { Id = 100, Code = "R1", Name = "Room 1", CampusId = 1, Capacity = 30 });
        db.Students.AddRange(
            new Student { Id = 1, StudentNumber = "S1", FirstName = "A", LastName = "A", Programme = prog },
            new Student { Id = 2, StudentNumber = "S2", FirstName = "B", LastName = "B", Programme = prog });
        var start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        db.Exams.AddRange(
            new Exam { Id = 50, Title = "Exam C1", CourseId = 10, TermId = 7, ScheduledStart = start, DurationMinutes = 120 },
            new Exam { Id = 51, Title = "Exam C2", CourseId = 11, TermId = 7, ScheduledStart = start.AddMinutes(60), DurationMinutes = 120 });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Allocating_more_seats_than_capacity_is_rejected()
    {
        using var db = NewDb();
        var svc = new ExamSchedulingService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AllocateRoomAsync(50, 100, 40, default)); // room holds 30
        var ok = await svc.AllocateRoomAsync(50, 100, 25, default);
        Assert.Equal(25, ok.SeatsAllocated);
    }

    [Fact]
    public async Task Detects_room_double_booking_for_overlapping_exams()
    {
        using var db = NewDb();
        var svc = new ExamSchedulingService(db);
        await svc.AllocateRoomAsync(50, 100, 20, default);
        await svc.AllocateRoomAsync(51, 100, 20, default);   // same room, overlapping times
        var clashes = await svc.DetectClashesAsync(7);
        Assert.Contains(clashes, c => c.Type == ClashType.RoomDoubleBooked);
    }

    [Fact]
    public async Task Detects_student_overlap_when_registered_for_two_overlapping_exams()
    {
        using var db = NewDb();
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered },
            new Enrollment { StudentId = 1, CourseId = 11, TermId = 7, Status = EnrollmentStatus.Registered });
        await db.SaveChangesAsync();
        var clashes = await new ExamSchedulingService(db).DetectClashesAsync(7);
        Assert.Contains(clashes, c => c.Type == ClashType.StudentOverlap && c.StudentId == 1);
    }

    [Fact]
    public async Task Detects_capacity_exceeded()
    {
        using var db = NewDb();
        var svc = new ExamSchedulingService(db);
        // 2 students registered for course 10, allocate only 1 seat
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered },
            new Enrollment { StudentId = 2, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered });
        await db.SaveChangesAsync();
        await svc.AllocateRoomAsync(50, 100, 1, default);
        var clashes = await svc.DetectClashesAsync(7);
        Assert.Contains(clashes, c => c.Type == ClashType.CapacityExceeded && c.ExamId == 50);
    }

    [Fact]
    public async Task Extra_time_arrangement_is_considered_in_student_overlap()
    {
        using var db = NewDb();
        // Make exams NOT overlap normally (C2 starts exactly when C1 ends), but extra time pushes C1 past it.
        var e1 = await db.Exams.FindAsync(50); var e2 = await db.Exams.FindAsync(51);
        e2!.ScheduledStart = e1!.ScheduledStart!.Value.AddMinutes(e1.DurationMinutes); // back-to-back, no overlap
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, Status = EnrollmentStatus.Registered },
            new Enrollment { StudentId = 1, CourseId = 11, TermId = 7, Status = EnrollmentStatus.Registered });
        db.SpecialArrangements.Add(new SpecialArrangement { StudentId = 1, Type = SpecialArrangementType.ExtraTime, ExtraTimePercent = 25, Approved = true });
        await db.SaveChangesAsync();
        var clashes = await new ExamSchedulingService(db).DetectClashesAsync(7);
        Assert.Contains(clashes, c => c.Type == ClashType.StudentOverlap && c.StudentId == 1); // extra time creates the overlap
    }
}
