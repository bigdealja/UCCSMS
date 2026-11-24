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
    }
}
