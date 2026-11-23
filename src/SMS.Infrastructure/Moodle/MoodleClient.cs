using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMS.Application.Moodle;

namespace SMS.Infrastructure.Moodle;

/// <summary>Talks to Moodle's Web Services REST API:
///   {BaseUrl}/webservice/rest/server.php?wstoken=..&moodlewsrestformat=json&wsfunction=..
/// All calls go over HTTPS (BaseUrl must be https). The token is injected per request and
/// never logged.</summary>
public class MoodleClient : IMoodleClient
{
    private readonly HttpClient _http;
    private readonly MoodleOptions _opt;
    public MoodleClient(HttpClient http, IOptions<MoodleOptions> opt) { _http = http; _opt = opt.Value; }

    string Url(string fn) =>
        $"{_opt.BaseUrl.TrimEnd('/')}/webservice/rest/server.php?wstoken={_opt.Token}&moodlewsrestformat=json&wsfunction={fn}";

    static void ThrowIfMoodleError(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("exception", out var ex))
            throw new InvalidOperationException($"Moodle error: {ex.GetString()} — {root.GetProperty("message").GetString()}");
    }

    async Task<JsonElement> PostAsync(string fn, IEnumerable<KeyValuePair<string, string>> form, CancellationToken ct)
    {
        using var resp = await _http.PostAsync(Url(fn), new FormUrlEncodedContent(form), ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        ThrowIfMoodleError(doc.RootElement);
        return doc.RootElement.Clone();
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try { await PostAsync("core_webservice_get_site_info", Array.Empty<KeyValuePair<string, string>>(), ct); return true; }
        catch { return false; }
    }

    public async Task<MoodleUser> EnsureUserAsync(string username, string email, string firstName, string lastName, CancellationToken ct = default)
    {
        // Look up by username first
        var found = await PostAsync("core_user_get_users_by_field",
            new[] { KV("field", "username"), KV("values[0]", username) }, ct);
        if (found.ValueKind == JsonValueKind.Array && found.GetArrayLength() > 0)
            return ToUser(found[0]);

        // Create
        var created = await PostAsync("core_user_create_users", new[]
        {
            KV("users[0][username]", username.ToLowerInvariant()),
            KV("users[0][email]", email),
            KV("users[0][firstname]", firstName),
            KV("users[0][lastname]", lastName),
            KV("users[0][idnumber]", username),
            KV("users[0][auth]", "oidc"),   // SSO via the shared Keycloak realm
        }, ct);
        var id = created[0].GetProperty("id").GetInt64();
        return new MoodleUser(id, username, email, firstName, lastName);
    }

    public async Task<MoodleCourseRef> EnsureCourseAsync(string shortName, string fullName, CancellationToken ct = default)
    {
        var found = await PostAsync("core_course_get_courses_by_field",
            new[] { KV("field", "shortname"), KV("value", shortName) }, ct);
        if (found.TryGetProperty("courses", out var courses) && courses.GetArrayLength() > 0)
        {
            var c = courses[0];
            return new MoodleCourseRef(c.GetProperty("id").GetInt64(), shortName, fullName);
        }
        var created = await PostAsync("core_course_create_courses", new[]
        {
            KV("courses[0][fullname]", fullName),
            KV("courses[0][shortname]", shortName),
            KV("courses[0][categoryid]", "1"),
        }, ct);
        return new MoodleCourseRef(created[0].GetProperty("id").GetInt64(), shortName, fullName);
    }

    public async Task EnrolUserAsync(long moodleUserId, long moodleCourseId, int roleId, CancellationToken ct = default)
        => await PostAsync("enrol_manual_enrol_users", new[]
        {
            KV("enrolments[0][roleid]", roleId.ToString()),
            KV("enrolments[0][userid]", moodleUserId.ToString()),
            KV("enrolments[0][courseid]", moodleCourseId.ToString()),
        }, ct);

    public async Task<IReadOnlyList<MoodleGrade>> GetCourseGradesAsync(long moodleCourseId, CancellationToken ct = default)
    {
        var root = await PostAsync("gradereport_user_get_grade_items",
            new[] { KV("courseid", moodleCourseId.ToString()) }, ct);
        var list = new List<MoodleGrade>();
        if (!root.TryGetProperty("usergrades", out var users)) return list;
        foreach (var u in users.EnumerateArray())
        {
            string idnum = u.TryGetProperty("useridnumber", out var idn) ? (idn.GetString() ?? "") : "";
            string? email = u.TryGetProperty("useremail", out var em) ? em.GetString() : null;
            double? final = null; string? letter = null;
            if (u.TryGetProperty("gradeitems", out var items))
                foreach (var gi in items.EnumerateArray())
                    if (gi.TryGetProperty("itemtype", out var it) && it.GetString() == "course")
                    {
                        if (gi.TryGetProperty("graderaw", out var gr) && gr.ValueKind == JsonValueKind.Number) final = gr.GetDouble();
                        if (gi.TryGetProperty("gradeformatted", out var gf)) letter = gf.GetString();
                    }
            list.Add(new MoodleGrade(idnum, email, letter, final));
        }
        return list;
    }

    static KeyValuePair<string, string> KV(string k, string v) => new(k, v);
    static MoodleUser ToUser(JsonElement e) => new(
        e.GetProperty("id").GetInt64(),
        e.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "",
        e.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
        e.TryGetProperty("firstname", out var fn) ? fn.GetString() ?? "" : "",
        e.TryGetProperty("lastname", out var ln) ? ln.GetString() ?? "" : "");
}
