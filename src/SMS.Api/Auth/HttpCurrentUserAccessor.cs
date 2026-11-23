using SMS.Infrastructure.Audit;

namespace SMS.Api.Auth;

/// <summary>Binds the audit current-user to the authenticated HTTP identity.</summary>
public sealed class HttpCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;
    public HttpCurrentUserAccessor(IHttpContextAccessor http) => _http = http;
    public string? UserName => _http.HttpContext?.User?.Identity?.Name;
}
