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
        await db.Database.MigrateAsync(ct);
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
        for (int i = 0; i < people.GetLength(0); i++)
            db.Students.Add(new Student {
                StudentNumber = people[i,0], FirstName = people[i,1], LastName = people[i,2],
                Email = $"{people[i,1].ToLower()}@example.edu", Status = StudentStatus.Active, Programme = prog });

        await db.SaveChangesAsync(ct);
    }
}
