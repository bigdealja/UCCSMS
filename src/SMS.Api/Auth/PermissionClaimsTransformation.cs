using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SMS.Infrastructure.Security;

namespace SMS.Api.Auth;

/// <summary>On each request, resolves the authenticated user's permissions and adds them as
/// "permission" claims so [HasPermission] can check them.</summary>
public sealed class PermissionClaimsTransformation : IClaimsTransformation
{
    private readonly PermissionResolver _resolver;
    public PermissionClaimsTransformation(PermissionResolver resolver) => _resolver = resolver;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.HasClaim(c => c.Type == "permission")) return principal;  // already transformed

        var perms = await _resolver.ResolveAsync(principal);
        if (perms.Count == 0) return principal;
        var identity = new ClaimsIdentity();
        foreach (var p in perms) identity.AddClaim(new Claim("permission", p));
        principal.AddIdentity(identity);
        return principal;
    }
}
