using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Pdf;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class PdfTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        GradeSchemeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }
    static PdfService Svc(SmsDbContext db) => new(db, new GradingService(db));
    static void AssertIsPdf(byte[] b)
    {
        Assert.True(b.Length > 500);
        Assert.Equal((byte)'%', b[0]); Assert.Equal((byte)'P', b[1]); Assert.Equal((byte)'D', b[2]); Assert.Equal((byte)'F', b[3]);
    }

    [Fact]
    public async Task Generates_a_transcript_pdf()
    {
        using var db = NewDb();
        var prog = new Programme { Id = 1, Code = "BSC", Name = "BSc CS", Level = ProgrammeLevel.Bachelors };
        var course = new Course { Id = 10, Code = "CS101", Title = "Intro", Programme = prog, Credits = 3 };
        db.Courses.Add(course);
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Students.Add(new Student { Id = 1, StudentNumber = "S1", FirstName = "Ada", LastName = "L", Programme = prog, Status = StudentStatus.Active });
        db.Enrollments.Add(new Enrollment { StudentId = 1, CourseId = 10, TermId = 7, LetterGrade = "A", FinalExamMark = 92 });
        await db.SaveChangesAsync();

        AssertIsPdf(await Svc(db).TranscriptAsync(1));
    }

    [Fact]
    public async Task Generates_an_acceptance_letter_pdf()
    {
        using var db = NewDb();
        var prog = new Programme { Id = 1, Code = "BSC", Name = "BSc CS", Level = ProgrammeLevel.Bachelors };
        db.Programmes.Add(prog);
        db.Terms.Add(new Term { Id = 7, Name = "Fall", Year = 2026 });
        db.Applications.Add(new AdmissionApplication { Id = 5, ApplicationNumber = "APP-1", FirstName = "Al", LastName = "T",
            ProgrammeId = 1, IntakeTermId = 7, Decision = AdmissionCategory.FullAcceptance, Status = ApplicationStatus.Accepted });
        await db.SaveChangesAsync();

        AssertIsPdf(await Svc(db).AcceptanceLetterAsync(5));
    }
}
