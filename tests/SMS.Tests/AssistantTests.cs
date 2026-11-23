using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Assistant;
using SMS.Infrastructure.Ai;
using SMS.Infrastructure.Accounting;
using SMS.Infrastructure.Finance;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class AssistantTests
{
    static SmsDbContext SeededDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        KnowledgeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        GradeSchemeSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }
    static AssistantService Svc(SmsDbContext db) =>
        new(db, new NullAiAssistant(), new GradingService(db), new FinanceService(db, new NullAccountingProvider()));

    [Fact]
    public async Task Retrieves_relevant_policy_and_falls_back_without_ai()
    {
        using var db = SeededDb();
        var ans = await Svc(db).AskAsync("How is my GPA calculated?", null, "student.jo");
        Assert.False(ans.UsedAi);
        Assert.Contains("GPA", ans.Answer);
        Assert.Contains(ans.Sources, s => s.Contains("GPA"));
        Assert.Equal(1, await db.AssistantQueryLogs.CountAsync());   // query logged
    }

    [Fact]
    public async Task Includes_students_own_record_facts_when_studentId_supplied()
    {
        using var db = SeededDb();
        var prog = new Programme { Id = 1, Code = "P", Name = "BSc", Level = ProgrammeLevel.Bachelors };
        db.Students.Add(new Student { Id = 1, StudentNumber = "S1", FirstName = "Ada", LastName = "L", Programme = prog, Status = StudentStatus.Active });
        await db.SaveChangesAsync();

        var ans = await Svc(db).AskAsync("What is my account balance?", 1, "student.ada");
        Assert.Contains("balance", ans.Answer, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ada", ans.Answer);
    }

    [Fact]
    public async Task Unknown_topic_directs_to_the_office()
    {
        using var db = SeededDb();
        var ans = await Svc(db).AskAsync("zzz qqq xyzzy", null, "jo");
        Assert.Contains("office", ans.Answer, System.StringComparison.OrdinalIgnoreCase);
    }
}
