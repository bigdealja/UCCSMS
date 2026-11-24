using System.Globalization;

namespace SMS.Infrastructure.DataMigration;

/// <summary>Reads legacy data from exported CSV files in a directory (campuses.csv, schools.csv,
/// programmes.csv, terms.csv, courses.csv, students.csv, enrollments.csv). Lets a migration run
/// offline from an Aeorion export before (or instead of) a direct DB connection.</summary>
public class CsvLegacySource : ILegacySource
{
    private readonly string _dir;
    public CsvLegacySource(string dir) => _dir = dir;

    public IReadOnlyList<LegacyCampus> Campuses() => Read("campuses.csv", f => new LegacyCampus(f["code"], f["name"]));
    public IReadOnlyList<LegacySchool> Schools() => Read("schools.csv", f => new LegacySchool(f["code"], f["name"], f["campuscode"]));
    public IReadOnlyList<LegacyProgramme> Programmes() => Read("programmes.csv", f => new LegacyProgramme(f["code"], f["name"], f["schoolcode"], Int(f, "creditsrequired"), Get(f, "level")));
    public IReadOnlyList<LegacyTerm> Terms() => Read("terms.csv", f => new LegacyTerm(f["name"], Int(f, "year"), Get(f, "type"), Date(f, "startdate"), Date(f, "enddate"), Bool(f, "isactive")));
    public IReadOnlyList<LegacyCourse> Courses() => Read("courses.csv", f => new LegacyCourse(f["code"], f["title"], Int(f, "credits"), f["programmecode"]));
    public IReadOnlyList<LegacyStudent> Students() => Read("students.csv", f => new LegacyStudent(f["studentnumber"], f["firstname"], f["lastname"], Get(f, "email"), Date(f, "dateofbirth"), Get(f, "status"), f["programmecode"]));
    public IReadOnlyList<LegacyEnrollment> Enrollments() => Read("enrollments.csv", f => new LegacyEnrollment(f["studentnumber"], f["coursecode"], f["termname"], Int(f, "termyear"), Get(f, "lettergrade"), Dbl(f, "courseworkmark"), Dbl(f, "finalexammark")));

    List<T> Read<T>(string file, Func<Dictionary<string, string>, T> map)
    {
        var path = Path.Combine(_dir, file);
        var list = new List<T>();
        if (!File.Exists(path)) return list;
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return list;
        var headers = Split(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = Split(lines[i]);
            var f = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length && j < cells.Count; j++) f[headers[j]] = cells[j];
            list.Add(map(f));
        }
        return list;
    }

    static string Get(Dictionary<string, string> f, string k) => f.TryGetValue(k, out var v) && v.Length > 0 ? v : null!;
    static int Int(Dictionary<string, string> f, string k) => f.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0;
    static double? Dbl(Dictionary<string, string> f, string k) => f.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null;
    static bool Bool(Dictionary<string, string> f, string k) => f.TryGetValue(k, out var v) && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
    static DateOnly? Date(Dictionary<string, string> f, string k) => f.TryGetValue(k, out var v) && DateOnly.TryParse(v, out var d) ? d : null;

    static List<string> Split(string line)
    {
        var r = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++) { var c = line[i];
            if (q) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else if (c == '"') q = false; else sb.Append(c); }
            else { if (c == '"') q = true; else if (c == ',') { r.Add(sb.ToString()); sb.Clear(); } else sb.Append(c); } }
        r.Add(sb.ToString()); return r;
    }
}
