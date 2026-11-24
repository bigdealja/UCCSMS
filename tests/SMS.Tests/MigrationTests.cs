using Microsoft.EntityFrameworkCore;
using SMS.Domain.Enums;
using SMS.Infrastructure.DataMigration;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class MigrationTests
{
    class FakeLegacy : ILegacySource
    {
        public IReadOnlyList<LegacyCampus> Campuses() => new[] { new LegacyCampus("MAIN", "Main Campus") };
        public IReadOnlyList<LegacySchool> Schools() => new[] { new LegacySchool("SCI", "Science", "MAIN") };
        public IReadOnlyList<LegacyProgramme> Programmes() => new[] { new LegacyProgramme("BSC-CS", "BSc CS", "SCI", 120, "Bachelors") };
        public IReadOnlyList<LegacyTerm> Terms() => new[] { new LegacyTerm("Fall", 2026, "Semester", null, null, true) };
        public IReadOnlyList<LegacyCourse> Courses() => new[] { new LegacyCourse("CS101", "Intro", 3, "BSC-CS") };
        public IReadOnlyList<LegacyStudent> Students() => new[] {
            new LegacyStudent("S0001", "Ada", "Lovelace", "ada@x.edu", null, "Active", "BSC-CS"),
            new LegacyStudent("S0002", "Alan", "Turing", null, null, "Active", "NOPE") };   // bad programme -> skipped
        public IReadOnlyList<LegacyEnrollment> Enrollments() => new[] {
            new LegacyEnrollment("S0001", "CS101", "Fall", 2026, "A", 88, 92) };

        public IReadOnlyList<LegacyFeeType> FeeTypes() => new[] { new LegacyFeeType("TUIT", "Tuition", 150000m, "4020-01-01") };
        public IReadOnlyList<LegacyFinanceTxn> FinanceTransactions() => new[] {
            new LegacyFinanceTxn("S0001", "CHG-1", "TUIT", "Charge", 150000m, null, "Tuition", new DateTimeOffset(2026,8,1,0,0,0,TimeSpan.Zero), "Fall", 2026),
            // legacy payment stored as a positive amount -> must become negative on the new ledger
            new LegacyFinanceTxn("S0001", "RCPT-1", null, "Payment", 90000m, "Card", "Part payment", new DateTimeOffset(2026,9,1,0,0,0,TimeSpan.Zero), null, null),
            new LegacyFinanceTxn("NOPE", "X", null, "Charge", 10m, null, null, null, null, null) };   // unknown student -> skipped
        public IReadOnlyList<LegacyLead> Leads() => new[] {
            new LegacyLead("L1", "Katherine", "Johnson", "kj@x.edu", null, "Website", "Qualified", "BSC-CS", "officer.jane") };
        public IReadOnlyList<LegacyApplication> Applications() => new[] {
            new LegacyApplication("APP-1", "Katherine", "Johnson", "kj@x.edu", null, "BSC-CS", "Fall", 2026, "Accepted", "FullAcceptance", "officer.jane", "manager.bob", new DateTimeOffset(2026,6,1,0,0,0,TimeSpan.Zero), new DateTimeOffset(2026,7,1,0,0,0,TimeSpan.Zero), "L1"),
            new LegacyApplication("APP-2", "Bad", "Term", null, null, "BSC-CS", "Nope", 1999, null, null, null, null, null, null, null) };  // unknown term -> skipped
    }

    static SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new SmsDbContext(opts);
    }

    [Fact]
    public void Mappers_translate_legacy_codes()
    {
        Assert.Equal(ProgrammeLevel.Masters, Mappers.MapLevel("graduate"));
        Assert.Equal(ProgrammeLevel.OccupationalDiploma, Mappers.MapLevel("Diploma"));
        Assert.Equal(StudentStatus.Graduated, Mappers.MapStudentStatus("G"));
    }

    [Fact]
    public async Task Dry_run_reports_without_writing()
    {
        using var db = NewDb();
        var report = await new MigrationRunner(db, new FakeLegacy()).RunAsync(dryRun: true);
        Assert.True(report.DryRun);
        Assert.Equal(1, report.For("Student").Inserted);     // S0001 would insert
        Assert.Equal(1, report.For("Student").Skipped);      // S0002 bad programme
        Assert.Equal(0, await db.Students.CountAsync());      // nothing written in a dry run
    }

    [Fact]
    public async Task Apply_loads_data_and_reconciles_with_a_reported_skip()
    {
        using var db = NewDb();
        var report = await new MigrationRunner(db, new FakeLegacy()).RunAsync(dryRun: false);

        Assert.Equal(1, await db.Campuses.CountAsync());
        Assert.Equal(1, await db.Students.CountAsync());      // only the valid student
        var e = await db.Enrollments.FirstAsync();
        Assert.Equal("A", e.LetterGrade);
        Assert.Equal("Migration", e.GradeSource);
        Assert.Equal(1, report.For("Student").Skipped);
        Assert.Single(report.For("Student").Errors);
    }

    [Fact]
    public async Task Re_running_updates_rather_than_duplicating()
    {
        using var db = NewDb();
        var src = new FakeLegacy();
        await new MigrationRunner(db, src).RunAsync(dryRun: false);
        await new MigrationRunner(db, src).RunAsync(dryRun: false);   // second run

        Assert.Equal(1, await db.Students.CountAsync());      // not duplicated
        Assert.Equal(1, await db.Courses.CountAsync());
        Assert.Equal(1, await db.Enrollments.CountAsync());
        Assert.Equal(2, await db.FinancialTransactions.CountAsync());  // ledger not duplicated
        Assert.Equal(1, await db.Leads.CountAsync());
        Assert.Equal(1, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task Csv_source_reads_finance_and_admissions_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "campuses.csv"), "code,name\nMAIN,Main Campus\n");
            File.WriteAllText(Path.Combine(dir, "schools.csv"), "code,name,campuscode\nSCI,Science,MAIN\n");
            File.WriteAllText(Path.Combine(dir, "programmes.csv"), "code,name,schoolcode,creditsrequired,level\nBSC-CS,BSc CS,SCI,120,Bachelors\n");
            File.WriteAllText(Path.Combine(dir, "terms.csv"), "name,year,type,startdate,enddate,isactive\nFall,2026,Semester,,,1\n");
            File.WriteAllText(Path.Combine(dir, "students.csv"), "studentnumber,firstname,lastname,email,status,programmecode\nS0001,Ada,Lovelace,ada@x.edu,Active,BSC-CS\n");
            File.WriteAllText(Path.Combine(dir, "feetypes.csv"), "code,name,defaultamount,distributioncode\nTUIT,Tuition,150000,4020-01-01\n");
            File.WriteAllText(Path.Combine(dir, "finance.csv"), "studentnumber,reference,feetypecode,type,amount,method,description,postedutc,termname,termyear\nS0001,RCPT-9,,Payment,90000,Card,Part payment,2026-09-01T00:00:00Z,Fall,2026\n");
            File.WriteAllText(Path.Combine(dir, "leads.csv"), "externalref,firstname,lastname,email,source,status,programmecode\nL9,Kat,John,kj@x.edu,Website,Qualified,BSC-CS\n");
            File.WriteAllText(Path.Combine(dir, "applications.csv"), "applicationnumber,firstname,lastname,programmecode,intaketermname,intaketermyear,status,decision,leadexternalref\nAPP-9,Kat,John,BSC-CS,Fall,2026,Accepted,FullAcceptance,L9\n");

            using var db = NewDb();
            var report = await new MigrationRunner(db, new CsvLegacySource(dir)).RunAsync(dryRun: false);

            var txn = await db.FinancialTransactions.SingleAsync();
            Assert.Equal(-90000m, txn.Amount);                 // payment read from CSV and signed
            Assert.Equal(PaymentMethod.Card, txn.Method);
            Assert.Equal("APP-9", (await db.Applications.SingleAsync()).ApplicationNumber);
            Assert.Equal(1, await db.Leads.CountAsync());
            Assert.True(report.Reconciled);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Finance_and_admissions_mappers_translate_codes()
    {
        Assert.Equal(TransactionType.Payment, Mappers.MapTransactionType("receipt"));
        Assert.Equal(PaymentMethod.BankTransfer, Mappers.MapPaymentMethod("wire"));
        Assert.Equal(-90000m, Mappers.SignedAmount(TransactionType.Payment, 90000m));   // sign flipped
        Assert.Equal(150000m, Mappers.SignedAmount(TransactionType.Charge, 150000m));
        Assert.Equal(LeadStatus.Qualified, Mappers.MapLeadStatus("qualified"));
        Assert.Equal(ApplicationStatus.Accepted, Mappers.MapApplicationStatus("admitted"));
        Assert.Equal(AdmissionCategory.FullAcceptance, Mappers.MapAdmissionCategory("full acceptance"));
    }

    [Fact]
    public async Task Apply_loads_finance_history_with_correct_signs_and_skips_unknown_student()
    {
        using var db = NewDb();
        var report = await new MigrationRunner(db, new FakeLegacy()).RunAsync(dryRun: false);

        Assert.Equal(1, await db.FeeTypes.CountAsync());
        var txns = await db.FinancialTransactions.OrderBy(t => t.PostedUtc).ToListAsync();
        Assert.Equal(2, txns.Count);                                   // the unknown-student row is skipped
        Assert.Equal(150000m, txns[0].Amount);                         // charge stays positive
        Assert.Equal(-90000m, txns[1].Amount);                         // payment becomes negative
        Assert.Equal(PaymentMethod.Card, txns[1].Method);
        Assert.Equal(1, report.For("FinancialTransaction").Skipped);
        // balance = sum of signed amounts
        Assert.Equal(60000m, txns.Sum(t => t.Amount));
    }

    [Fact]
    public async Task Apply_loads_admissions_history_and_links_application_to_lead()
    {
        using var db = NewDb();
        var report = await new MigrationRunner(db, new FakeLegacy()).RunAsync(dryRun: false);

        var lead = await db.Leads.SingleAsync();
        Assert.Equal("L1", lead.ExternalCrmId);
        Assert.Equal(LeadStatus.Qualified, lead.Status);

        var app = await db.Applications.SingleAsync();                 // APP-2 skipped (unknown term)
        Assert.Equal("APP-1", app.ApplicationNumber);
        Assert.Equal(ApplicationStatus.Accepted, app.Status);
        Assert.Equal(AdmissionCategory.FullAcceptance, app.Decision);
        Assert.Equal(lead.Id, app.LeadId);                             // linked by legacy ref
        Assert.Equal(1, report.For("Application").Skipped);
    }
}
