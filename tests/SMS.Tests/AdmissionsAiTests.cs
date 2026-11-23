using Microsoft.EntityFrameworkCore;
using SMS.Application.Ai;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Admissions;
using SMS.Infrastructure.Ai;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

/// <summary>Stands in for a configured LLM: returns canned JSON so we can test parsing + storage
/// without any real AI call.</summary>
class FakeAiAssistant : IAiAssistant
{
    private readonly string _json;
    public FakeAiAssistant(string json) => _json = json;
    public string Name => "Fake";
    public bool IsConfigured => true;
    public Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
        => Task.FromResult("Here is my recommendation: " + _json);
}

public class AdmissionsAiTests
{
    static SmsDbContext NewDb(ProgrammeLevel level = ProgrammeLevel.Bachelors, bool mature = false)
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "P", Name = "Prog", Level = level };
        db.Programmes.Add(prog);
        db.Terms.Add(new Term { Id = 1, Name = "Fall", Year = 2026 });
        var app = new AdmissionApplication { Id = 5, ApplicationNumber = "APP-1", FirstName = "Ada", LastName = "L",
            ProgrammeId = 1, IntakeTermId = 1, IsMatureEntrant = mature, Status = ApplicationStatus.UnderReview };
        db.Applications.Add(app);
        db.ApplicationDocuments.Add(new ApplicationDocument { ApplicationId = 5, DocType = "Transcript", Required = true, Received = false });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Ai_recommendation_is_parsed_and_stored_as_advisory()
    {
        using var db = NewDb();
        var ai = new FakeAiAssistant("""{"category":"FullAcceptance","confidence":0.9,"rationale":"5 subjects incl. English & Math at grade I","missingDocuments":["Transcript"]}""");
        var svc = new AdmissionsAiService(db, ai);

        var rec = await svc.RecommendAsync(5);

        Assert.Equal("AI", rec.Source);
        Assert.Equal(AdmissionCategory.FullAcceptance, rec.RecommendedCategory);
        Assert.Equal(0.9, rec.Confidence, 2);
        Assert.Equal(1, await db.AdmissionRecommendations.CountAsync());

        // Crucially: the application decision is NOT changed by the recommendation.
        var app = await db.Applications.FindAsync(5);
        Assert.Equal(ApplicationStatus.UnderReview, app!.Status);
        Assert.Equal(AdmissionCategory.None, app.Decision);
    }

    [Fact]
    public async Task Falls_back_to_rules_when_ai_disabled()
    {
        using var db = NewDb(mature: true);
        var svc = new AdmissionsAiService(db, new NullAiAssistant());
        var rec = await svc.RecommendAsync(5);
        Assert.Equal("Rules", rec.Source);
        Assert.Equal(AdmissionCategory.ConditionalClass2Mature, rec.RecommendedCategory);   // mature-entry route
    }

    [Fact]
    public async Task Graduate_rules_recommend_provisional()
    {
        using var db = NewDb(level: ProgrammeLevel.Masters);
        var svc = new AdmissionsAiService(db, new NullAiAssistant());
        var rec = await svc.RecommendAsync(5);
        Assert.Equal(AdmissionCategory.Provisional, rec.RecommendedCategory);
    }
}
