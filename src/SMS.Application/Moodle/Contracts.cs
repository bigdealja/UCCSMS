namespace SMS.Application.Moodle;

/// <summary>Config for the institution's Moodle Web Services endpoint.
/// Token is a Moodle external-service token — supplied via env/secret, never hard-coded.</summary>
public class MoodleOptions
{
    public string BaseUrl { get; set; } = "";     // e.g. https://moodle.your-institution.edu
    public string Token { get; set; } = "";        // Moodle web-service token
    public int StudentRoleId { get; set; } = 5;     // Moodle's default "student" role id
}

public record MoodleUser(long Id, string Username, string Email, string FirstName, string LastName);
public record MoodleCourseRef(long Id, string ShortName, string FullName);
public record MoodleGrade(string UserIdNumber, string? Email, string? LetterGrade, double? FinalMark);

/// <summary>Abstraction over Moodle's REST API. Implemented in Infrastructure with HttpClient;
/// kept as an interface so sync logic and tests don't depend on HTTP or a live Moodle.</summary>
public interface IMoodleClient
{
    Task<bool> PingAsync(CancellationToken ct = default);
    Task<MoodleUser> EnsureUserAsync(string username, string email, string firstName, string lastName, CancellationToken ct = default);
    Task<MoodleCourseRef> EnsureCourseAsync(string shortName, string fullName, CancellationToken ct = default);
    Task EnrolUserAsync(long moodleUserId, long moodleCourseId, int roleId, CancellationToken ct = default);
    Task<IReadOnlyList<MoodleGrade>> GetCourseGradesAsync(long moodleCourseId, CancellationToken ct = default);
}

public record ProvisionResult(long MoodleCourseId, int UsersEnrolled, int AlreadyLinked);
public record GradePullResult(Guid BatchId, int GradesWritten, int Unmatched);
