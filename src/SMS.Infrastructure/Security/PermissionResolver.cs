using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Security;

/// <summary>Resolves the set of permission keys for a signed-in user, from (1) role claims in their
/// token (IdP roles) and (2) role assignments stored in the DB, mapped through role→permission.</summary>
public class PermissionResolver
{
    private readonly SmsDbContext _db;
    public PermissionResolver(SmsDbContext db) => _db = db;

    public async Task<HashSet<string>> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var roleNames = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(user.FindAll("role").Select(c => c.Value))
            .Concat(user.FindAll("roles").Select(c => c.Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var username = user.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
            foreach (var r in await _db.UserRoleAssignments.AsNoTracking()
                        .Where(a => a.Username == username).Select(a => a.RoleName).ToListAsync(ct))
                roleNames.Add(r);

        if (roleNames.Count == 0) return new HashSet<string>();

        var perms = await _db.Roles.AsNoTracking().Where(r => roleNames.Contains(r.Name))
            .SelectMany(r => r.Permissions.Select(p => p.Permission.Key)).Distinct().ToListAsync(ct);
        return perms.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
