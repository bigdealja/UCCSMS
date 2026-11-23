using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Admissions;
using SMS.Infrastructure.Crm;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class AdmissionsTests
{
    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        var prog = new Programme { Id = 1, Code = "BSC", Name = "BSc CS", Level = ProgrammeLevel.Bachelors };
        db.Terms.Add(new Term { Id = 1, Name = "Fall", Year = 2026 });
        db.Programmes.Add(prog);
        db.SaveChanges();
        return db;
    }
    static AdmissionsService Svc(SmsDbContext db) => new(db, new BuiltInCrmProvider());

    [Fact]
    public async Task Lead_converts_to_application_and_submit_sets_offer_state()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var lead = await svc.CreateLeadAsync(new Lead { FirstName = "Ada", LastName = "L", Email = "ada@x.edu", Source = "Website" });
        var app = await svc.ConvertLeadToApplicationAsync(lead.Id, 1, 1);
        Assert.Equal(LeadStatus.ApplicationStarted, (await db.Leads.FindAsync(lead.Id))!.Status);

        await svc.SubmitAsync(app.Id);
        var reloaded = await db.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Submitted, reloaded!.Status);      // automatic Offer of Admission
        Assert.NotNull(reloaded.SubmittedUtc);
    }

    [Fact]
    public async Task Decision_requires_a_second_person()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var lead = await svc.CreateLeadAsync(new Lead { FirstName = "Al", LastName = "T" });
        var app = await svc.ConvertLeadToApplicationAsync(lead.Id, 1, 1);
        await svc.SubmitAsync(app.Id);
        await svc.StartReviewAsync(app.Id, "officer.jane");

        // same person who reviewed cannot decide
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordDecisionAsync(app.Id, AdmissionCategory.FullAcceptance, "officer.jane", null));

        // a different approver can
        var decided = await svc.RecordDecisionAsync(app.Id, AdmissionCategory.FullAcceptance, "manager.bob", "ok");
        Assert.Equal(ApplicationStatus.Accepted, decided.Status);
        Assert.Equal(AdmissionCategory.FullAcceptance, decided.Decision);
    }

    [Fact]
    public async Task Accepted_application_converts_to_a_student()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var lead = await svc.CreateLeadAsync(new Lead { FirstName = "Grace", LastName = "H", Email = "grace@x.edu" });
        var app = await svc.ConvertLeadToApplicationAsync(lead.Id, 1, 1);
        await svc.SubmitAsync(app.Id);
        await svc.StartReviewAsync(app.Id, "officer.jane");
        await svc.RecordDecisionAsync(app.Id, AdmissionCategory.ConditionalClass1, "manager.bob", null);
        await svc.RecordOfferResponseAsync(app.Id, OfferResponse.Accepted);

        var student = await svc.ConvertToStudentAsync(app.Id, "S2026-001");
        Assert.Equal("S2026-001", student.StudentNumber);
        Assert.Equal(1, student.ProgrammeId);
        Assert.Equal(StudentStatus.Accepted, student.Status);

        var finalApp = await db.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Enrolled, finalApp!.Status);
        Assert.Equal(student.Id, finalApp.StudentId);
        Assert.Equal(LeadStatus.Converted, (await db.Leads.FindAsync(lead.Id))!.Status);
    }

    [Fact]
    public async Task Cannot_convert_an_undecided_application()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var lead = await svc.CreateLeadAsync(new Lead { FirstName = "No", LastName = "Decision" });
        var app = await svc.ConvertLeadToApplicationAsync(lead.Id, 1, 1);
        await svc.SubmitAsync(app.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConvertToStudentAsync(app.Id, "S2026-002"));
    }
}
