using Microsoft.Data.SqlClient;

namespace SMS.Infrastructure.DataMigration;

/// <summary>Reads legacy data directly from the Aeorion SQL Server. The SELECTs target the
/// reverse-engineered view/table names (sms_SI_V_Students, sms_ADM_D_Term, Courses, …) and are
/// overridable, since exact columns vary per install — adjust the query strings to the live schema.</summary>
public class SqlLegacySource : ILegacySource
{
    private readonly string _connString;
    public SqlLegacySource(string connString) => _connString = connString;

    // Adjust these to the actual Aeorion schema as needed.
    public string CampusQuery   = "SELECT Code, Name FROM dbo.sms_ADM_VA_Campuses";
    public string SchoolQuery   = "SELECT Code, Name, CampusCode FROM dbo.sms_BMS_VA_Colleges";
    public string ProgrammeQuery= "SELECT Code, Name, SchoolCode, CreditsRequired, Level FROM dbo.sms_ADM_V_StudentProgramList";
    public string TermQuery     = "SELECT Name, [Year], [Type], StartDate, EndDate, IsActive FROM dbo.sms_ADM_D_Term";
    public string CourseQuery   = "SELECT Code, Title, Credits, ProgrammeCode FROM dbo.Courses";
    public string StudentQuery  = "SELECT StudentNumber, FirstName, LastName, Email, DateOfBirth, [Status], ProgrammeCode FROM dbo.sms_SI_V_Students";
    public string EnrollQuery   = "SELECT StudentNumber, CourseCode, TermName, TermYear, LetterGrade, CourseworkMark, FinalExamMark FROM dbo.sms_ACA_V_StudentCourses";

    // Finance history (legacy ASM/finance ledger — adjust to your schema).
    public string FeeTypeQuery  = "SELECT Code, Name, DefaultAmount, DistributionCode FROM dbo.sms_FIN_D_FeeTypes";
    public string FinanceQuery  = "SELECT StudentNumber, Reference, FeeTypeCode, [Type], Amount, Method, Description, PostedUtc, TermName, TermYear FROM dbo.sms_FIN_V_StudentLedger";
    // Admissions history (legacy SMS_Apply — adjust to your schema).
    public string LeadQuery     = "SELECT ExternalRef, FirstName, LastName, Email, Phone, Source, [Status], ProgrammeCode, AssignedTo FROM dbo.app_Leads";
    public string ApplicationQuery = "SELECT ApplicationNumber, FirstName, LastName, Email, DateOfBirth, ProgrammeCode, IntakeTermName, IntakeTermYear, [Status], Decision, AssignedOfficer, DecidedBy, SubmittedUtc, DecidedUtc, LeadExternalRef FROM dbo.app_Applications";

    List<T> Query<T>(string sql, Func<SqlDataReader, T> map)
    {
        var list = new List<T>();
        using var conn = new SqlConnection(_connString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    static string S(SqlDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString()?.Trim() ?? ""; }
    static string? SN(SqlDataReader r, string c) { var v = S(r, c); return v.Length == 0 ? null : v; }
    static int I(SqlDataReader r, string c) => int.TryParse(S(r, c), out var n) ? n : 0;
    static double? D(SqlDataReader r, string c) => double.TryParse(S(r, c), out var n) ? n : (double?)null;
    static DateOnly? Dt(SqlDataReader r, string c) => DateOnly.TryParse(S(r, c), out var d) ? d : (DateOnly?)null;
    static bool B(SqlDataReader r, string c) { var v = S(r, c); return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); }
    static decimal M(SqlDataReader r, string c) => decimal.TryParse(S(r, c), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0m;
    static int? NI(SqlDataReader r, string c) => int.TryParse(S(r, c), out var n) ? n : (int?)null;
    static DateTimeOffset? Dto(SqlDataReader r, string c)
    {
        var i = r.GetOrdinal(c);
        if (r.IsDBNull(i)) return null;
        var v = r.GetValue(i);
        return v switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => DateTimeOffset.TryParse(v?.ToString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var p) ? p : (DateTimeOffset?)null
        };
    }

    public IReadOnlyList<LegacyCampus> Campuses() => Query(CampusQuery, r => new LegacyCampus(S(r, "Code"), S(r, "Name")));
    public IReadOnlyList<LegacySchool> Schools() => Query(SchoolQuery, r => new LegacySchool(S(r, "Code"), S(r, "Name"), S(r, "CampusCode")));
    public IReadOnlyList<LegacyProgramme> Programmes() => Query(ProgrammeQuery, r => new LegacyProgramme(S(r, "Code"), S(r, "Name"), S(r, "SchoolCode"), I(r, "CreditsRequired"), SN(r, "Level")));
    public IReadOnlyList<LegacyTerm> Terms() => Query(TermQuery, r => new LegacyTerm(S(r, "Name"), I(r, "Year"), SN(r, "Type"), Dt(r, "StartDate"), Dt(r, "EndDate"), B(r, "IsActive")));
    public IReadOnlyList<LegacyCourse> Courses() => Query(CourseQuery, r => new LegacyCourse(S(r, "Code"), S(r, "Title"), I(r, "Credits"), S(r, "ProgrammeCode")));
    public IReadOnlyList<LegacyStudent> Students() => Query(StudentQuery, r => new LegacyStudent(S(r, "StudentNumber"), S(r, "FirstName"), S(r, "LastName"), SN(r, "Email"), Dt(r, "DateOfBirth"), SN(r, "Status"), S(r, "ProgrammeCode")));
    public IReadOnlyList<LegacyEnrollment> Enrollments() => Query(EnrollQuery, r => new LegacyEnrollment(S(r, "StudentNumber"), S(r, "CourseCode"), S(r, "TermName"), I(r, "TermYear"), SN(r, "LetterGrade"), D(r, "CourseworkMark"), D(r, "FinalExamMark")));

    public IReadOnlyList<LegacyFeeType> FeeTypes() => Query(FeeTypeQuery, r => new LegacyFeeType(S(r, "Code"), S(r, "Name"), M(r, "DefaultAmount"), SN(r, "DistributionCode")));
    public IReadOnlyList<LegacyFinanceTxn> FinanceTransactions() => Query(FinanceQuery, r => new LegacyFinanceTxn(
        S(r, "StudentNumber"), SN(r, "Reference"), SN(r, "FeeTypeCode"), SN(r, "Type"), M(r, "Amount"),
        SN(r, "Method"), SN(r, "Description"), Dto(r, "PostedUtc"), SN(r, "TermName"), NI(r, "TermYear")));
    public IReadOnlyList<LegacyLead> Leads() => Query(LeadQuery, r => new LegacyLead(
        S(r, "ExternalRef"), S(r, "FirstName"), S(r, "LastName"), SN(r, "Email"), SN(r, "Phone"),
        SN(r, "Source"), SN(r, "Status"), SN(r, "ProgrammeCode"), SN(r, "AssignedTo")));
    public IReadOnlyList<LegacyApplication> Applications() => Query(ApplicationQuery, r => new LegacyApplication(
        S(r, "ApplicationNumber"), S(r, "FirstName"), S(r, "LastName"), SN(r, "Email"), Dt(r, "DateOfBirth"),
        S(r, "ProgrammeCode"), SN(r, "IntakeTermName"), NI(r, "IntakeTermYear"), SN(r, "Status"), SN(r, "Decision"),
        SN(r, "AssignedOfficer"), SN(r, "DecidedBy"), Dto(r, "SubmittedUtc"), Dto(r, "DecidedUtc"), SN(r, "LeadExternalRef")));
}
