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

    public IReadOnlyList<LegacyCampus> Campuses() => Query(CampusQuery, r => new LegacyCampus(S(r, "Code"), S(r, "Name")));
    public IReadOnlyList<LegacySchool> Schools() => Query(SchoolQuery, r => new LegacySchool(S(r, "Code"), S(r, "Name"), S(r, "CampusCode")));
    public IReadOnlyList<LegacyProgramme> Programmes() => Query(ProgrammeQuery, r => new LegacyProgramme(S(r, "Code"), S(r, "Name"), S(r, "SchoolCode"), I(r, "CreditsRequired"), SN(r, "Level")));
    public IReadOnlyList<LegacyTerm> Terms() => Query(TermQuery, r => new LegacyTerm(S(r, "Name"), I(r, "Year"), SN(r, "Type"), Dt(r, "StartDate"), Dt(r, "EndDate"), B(r, "IsActive")));
    public IReadOnlyList<LegacyCourse> Courses() => Query(CourseQuery, r => new LegacyCourse(S(r, "Code"), S(r, "Title"), I(r, "Credits"), S(r, "ProgrammeCode")));
    public IReadOnlyList<LegacyStudent> Students() => Query(StudentQuery, r => new LegacyStudent(S(r, "StudentNumber"), S(r, "FirstName"), S(r, "LastName"), SN(r, "Email"), Dt(r, "DateOfBirth"), SN(r, "Status"), S(r, "ProgrammeCode")));
    public IReadOnlyList<LegacyEnrollment> Enrollments() => Query(EnrollQuery, r => new LegacyEnrollment(S(r, "StudentNumber"), S(r, "CourseCode"), S(r, "TermName"), I(r, "TermYear"), SN(r, "LetterGrade"), D(r, "CourseworkMark"), D(r, "FinalExamMark")));
}
