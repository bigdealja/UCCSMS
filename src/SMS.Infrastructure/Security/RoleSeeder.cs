using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Security;

public static class RoleSeeder
{
    public static async Task EnsureSeededAsync(SmsDbContext db, CancellationToken ct = default)
    {
        if (await db.Roles.IgnoreQueryFilters().AnyAsync(ct)) return;

        var perms = Permissions.All.ToDictionary(p => p.Key, p => new Permission { Key = p.Key, Description = p.Desc });
        db.Permissions.AddRange(perms.Values);
        foreach (var (roleName, keys) in Permissions.RoleMap)
        {
            var role = new Role { Name = roleName };
            foreach (var k in keys) role.Permissions.Add(new RolePermission { Permission = perms[k] });
            db.Roles.Add(role);
        }
        await db.SaveChangesAsync(ct);
    }
}
