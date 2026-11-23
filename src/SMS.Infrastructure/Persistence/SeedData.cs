using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;

namespace SMS.Infrastructure.Persistence;

/// <summary>Seeds a little demo data in Development so the UI isn't empty.
/// Idempotent: does nothing if students already exist.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(SmsDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsRelational()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);
        if (await db.Students.IgnoreQueryFilters().AnyAsync(ct)) return;

        var campus = new Campus { Code = "MAIN", Name = "Main Campus" };
        var school = new School { Code = "SCI", Name = "School of Science", Campus = campus };
        var prog = new Programme { Code = "BSC-CS", Name = "BSc Computer Science", School = school, CreditsRequired = 120 };
        var term = new Term { Name = "Fall", Year = 2026, Type = TermType.Semester, IsActive = true,
                              StartDate = new DateOnly(2026, 8, 25), EndDate = new DateOnly(2026, 12, 15) };
        var c1 = new Course { Code = "CS101", Title = "Intro to Programming", Credits = 3, Programme = prog };
        var c2 = new Course { Code = "CS102", Title = "Data Structures", Credits = 3, Programme = prog };
        db.AddRange(campus, school, prog, term, c1, c2);

        string[,] people = {
            {"S0001","Ada","Lovelace"}, {"S0002","Alan","Turing"}, {"S0003","Grace","Hopper"},
            {"S0004","Linus","Torvalds"}, {"S0005","Margaret","Hamilton"}
        };
        var students = new List<Student>();
        for (int i = 0; i < people.GetLength(0); i++)
        {
            var s = new Student {
                StudentNumber = people[i,0], FirstName = people[i,1], LastName = people[i,2],
                Email = $"{people[i,1].ToLower()}@example.edu", Status = StudentStatus.Active, Programme = prog };
            students.Add(s); db.Students.Add(s);
        }
        await db.SaveChangesAsync(ct);

        // Registrations + a couple of grades
        db.Enrollments.AddRange(
            new Enrollment { StudentId = students[0].Id, CourseId = c1.Id, TermId = term.Id, Status = EnrollmentStatus.Registered, LetterGrade = "A", FinalExamMark = 92 },
            new Enrollment { StudentId = students[1].Id, CourseId = c1.Id, TermId = term.Id, Status = EnrollmentStatus.Registered, LetterGrade = "B", FinalExamMark = 71 },
            new Enrollment { StudentId = students[0].Id, CourseId = c2.Id, TermId = term.Id, Status = EnrollmentStatus.Registered });

        // Admissions: a lead + two applications
        var lead = new Lead { FirstName = "Katherine", LastName = "Johnson", Email = "kjohnson@example.edu", Source = "Website", Status = LeadStatus.Qualified, ProgrammeOfInterestId = prog.Id };
        db.Leads.Add(lead);
        db.Applications.AddRange(
            new AdmissionApplication { ApplicationNumber = "APP-2026-A1B2C3", FirstName = "Katherine", LastName = "Johnson",
                Email = "kjohnson@example.edu", ProgrammeId = prog.Id, IntakeTermId = term.Id, Status = ApplicationStatus.UnderReview, AssignedOfficer = "officer.jane" },
            new AdmissionApplication { ApplicationNumber = "APP-2026-D4E5F6", FirstName = "Dorothy", LastName = "Vaughan",
                Email = "dvaughan@example.edu", ProgrammeId = prog.Id, IntakeTermId = term.Id, Status = ApplicationStatus.Accepted, Decision = AdmissionCategory.FullAcceptance, DecidedBy = "manager.bob" });

        // Exams + a room
        db.Rooms.Add(new Room { Code = "A101", Name = "Lecture Hall A", CampusId = campus.Id, Capacity = 120 });
        db.Exams.AddRange(
            new Exam { Title = "CS101 Final", CourseId = c1.Id, TermId = term.Id, ScheduledStart = new DateTimeOffset(2026,12,8,9,0,0,TimeSpan.Zero), DurationMinutes = 120, Room = "A101", Status = ExamStatus.Scheduled },
            new Exam { Title = "CS102 Final", CourseId = c2.Id, TermId = term.Id, ScheduledStart = new DateTimeOffset(2026,12,8,9,0,0,TimeSpan.Zero), DurationMinutes = 120, Room = "A101", Status = ExamStatus.Scheduled });

        // Finance: a fee type + a charge and a payment on student 1
        var fee = new FeeType { Code = "TUIT", Name = "Tuition", DefaultAmount = 150000m, DistributionCode = "4020-01-01" };
        db.FeeTypes.Add(fee);
        await db.SaveChangesAsync(ct);
        db.FinancialTransactions.AddRange(
            new FinancialTransaction { StudentId = students[0].Id, FeeTypeId = fee.Id, Type = TransactionType.Charge, Amount = 150000m, Description = "Tuition", PostedUtc = DateTimeOffset.UtcNow.AddDays(-20) },
            new FinancialTransaction { StudentId = students[0].Id, Type = TransactionType.Payment, Amount = -90000m, Method = PaymentMethod.Card, Reference = "RCPT-1001", Description = "Part payment", PostedUtc = DateTimeOffset.UtcNow.AddDays(-5) });

        await db.SaveChangesAsync(ct);
    }
}
