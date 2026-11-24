using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.DataMigration;

/// <summary>Staged, idempotent migration from a legacy source into the new schema. Loads structural
/// data first (campuses → schools → programmes → terms → courses), then students and enrolments,
/// resolving relationships by natural key. Re-runnable: existing rows are updated, not duplicated.
/// With dryRun, nothing is written — only the reconciliation report is produced.</summary>
public class MigrationRunner
{
    private readonly SmsDbContext _db;
    private readonly ILegacySource _src;
    public MigrationRunner(SmsDbContext db, ILegacySource src) { _db = db; _src = src; }

    public async Task<ReconciliationReport> RunAsync(bool dryRun = false, CancellationToken ct = default)
    {
        var report = new ReconciliationReport { DryRun = dryRun };

        // Campuses
        var campusByCode = await _db.Campuses.IgnoreQueryFilters().ToDictionaryAsync(c => c.Code, c => c, ct);
        var rc = report.For("Campus");
        foreach (var r in _src.Campuses())
        {
            rc.Source++;
            if (campusByCode.TryGetValue(r.Code, out var e)) { e.Name = r.Name; rc.Updated++; }
            else { var e2 = new Campus { Code = r.Code.Trim(), Name = r.Name.Trim() }; _db.Campuses.Add(e2); campusByCode[e2.Code] = e2; rc.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Schools (resolve campus)
        var schoolByCode = await _db.Schools.IgnoreQueryFilters().ToDictionaryAsync(s => s.Code, s => s, ct);
        var rs = report.For("School");
        foreach (var r in _src.Schools())
        {
            rs.Source++;
            if (!campusByCode.TryGetValue(r.CampusCode, out var campus)) { rs.Skipped++; rs.Errors.Add($"School '{r.Code}': unknown campus '{r.CampusCode}'."); continue; }
            if (schoolByCode.TryGetValue(r.Code, out var e)) { e.Name = r.Name; e.Campus = campus; rs.Updated++; }
            else { var e2 = new School { Code = r.Code.Trim(), Name = r.Name.Trim(), Campus = campus }; _db.Schools.Add(e2); schoolByCode[e2.Code] = e2; rs.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Programmes (resolve school)
        var progByCode = await _db.Programmes.IgnoreQueryFilters().ToDictionaryAsync(p => p.Code, p => p, ct);
        var rp = report.For("Programme");
        foreach (var r in _src.Programmes())
        {
            rp.Source++;
            if (!schoolByCode.TryGetValue(r.SchoolCode, out var school)) { rp.Skipped++; rp.Errors.Add($"Programme '{r.Code}': unknown school '{r.SchoolCode}'."); continue; }
            if (progByCode.TryGetValue(r.Code, out var e)) { e.Name = r.Name; e.CreditsRequired = r.CreditsRequired; e.Level = Mappers.MapLevel(r.Level); rp.Updated++; }
            else { var e2 = Mappers.ToProgramme(r, 0); e2.School = school; _db.Programmes.Add(e2); progByCode[e2.Code] = e2; rp.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Terms (key: name+year)
        var termByKey = await _db.Terms.IgnoreQueryFilters().ToDictionaryAsync(t => t.Name + "|" + t.Year, t => t, ct);
        var rt = report.For("Term");
        foreach (var r in _src.Terms())
        {
            rt.Source++;
            var key = r.Name + "|" + r.Year;
            if (termByKey.TryGetValue(key, out var e)) { e.IsActive = r.IsActive; rt.Updated++; }
            else { var e2 = Mappers.ToTerm(r); _db.Terms.Add(e2); termByKey[key] = e2; rt.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Courses (resolve programme)
        var courseByCode = await _db.Courses.IgnoreQueryFilters().ToDictionaryAsync(c => c.Code, c => c, ct);
        var rcr = report.For("Course");
        foreach (var r in _src.Courses())
        {
            rcr.Source++;
            if (!progByCode.TryGetValue(r.ProgrammeCode, out var prog)) { rcr.Skipped++; rcr.Errors.Add($"Course '{r.Code}': unknown programme '{r.ProgrammeCode}'."); continue; }
            if (courseByCode.TryGetValue(r.Code, out var e)) { e.Title = r.Title; e.Credits = r.Credits; rcr.Updated++; }
            else { var e2 = Mappers.ToCourse(r, 0); e2.Programme = prog; _db.Courses.Add(e2); courseByCode[e2.Code] = e2; rcr.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Students (resolve programme)
        var studentByNumber = await _db.Students.IgnoreQueryFilters().ToDictionaryAsync(s => s.StudentNumber, s => s, ct);
        var rst = report.For("Student");
        foreach (var r in _src.Students())
        {
            rst.Source++;
            if (!progByCode.TryGetValue(r.ProgrammeCode, out var prog)) { rst.Skipped++; rst.Errors.Add($"Student '{r.StudentNumber}': unknown programme '{r.ProgrammeCode}'."); continue; }
            if (studentByNumber.TryGetValue(r.StudentNumber, out var e))
            { e.FirstName = r.FirstName; e.LastName = r.LastName; e.Email = r.Email; e.Status = Mappers.MapStudentStatus(r.Status); rst.Updated++; }
            else { var e2 = Mappers.ToStudent(r, 0); e2.Programme = prog; _db.Students.Add(e2); studentByNumber[e2.StudentNumber] = e2; rst.Inserted++; }
        }
        await SaveAsync(dryRun, ct);

        // Enrollments (resolve student + course + term)
        var enrolKey = (int sId, int cId, int tId) => $"{sId}|{cId}|{tId}";
        var existingEnrol = await _db.Enrollments.IgnoreQueryFilters().ToDictionaryAsync(e => e.StudentId + "|" + e.CourseId + "|" + e.TermId, e => e, ct);
        var re = report.For("Enrollment");
        foreach (var r in _src.Enrollments())
        {
            re.Source++;
            if (!studentByNumber.TryGetValue(r.StudentNumber, out var s)) { re.Skipped++; re.Errors.Add($"Enrollment: unknown student '{r.StudentNumber}'."); continue; }
            if (!courseByCode.TryGetValue(r.CourseCode, out var c)) { re.Skipped++; re.Errors.Add($"Enrollment: unknown course '{r.CourseCode}'."); continue; }
            if (!termByKey.TryGetValue(r.TermName + "|" + r.TermYear, out var t)) { re.Skipped++; re.Errors.Add($"Enrollment: unknown term '{r.TermName} {r.TermYear}'."); continue; }
            var key = enrolKey(s.Id, c.Id, t.Id);
            if (existingEnrol.TryGetValue(key, out var e))
            { e.LetterGrade = r.LetterGrade; e.CourseworkMark = r.CourseworkMark; e.FinalExamMark = r.FinalExamMark; e.GradeSource = "Migration"; re.Updated++; }
            else
            {
                var e2 = new Enrollment { Student = s, Course = c, Term = t, Status = Domain.Enums.EnrollmentStatus.Registered,
                    LetterGrade = r.LetterGrade, CourseworkMark = r.CourseworkMark, FinalExamMark = r.FinalExamMark, GradeSource = "Migration" };
                _db.Enrollments.Add(e2); re.Inserted++;
            }
        }
        await SaveAsync(dryRun, ct);

        return report;
    }

    async Task SaveAsync(bool dryRun, CancellationToken ct)
    {
        if (dryRun) { _db.ChangeTracker.Clear(); return; }  // discard staged changes in a dry run
        await _db.SaveChangesAsync(ct);
    }
}
