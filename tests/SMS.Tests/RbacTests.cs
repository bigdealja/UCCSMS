using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Security;
using Xunit;

namespace SMS.Tests;

public class RbacTests
{
    static SmsDbContext SeededDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SmsDbContext(opts);
        RoleSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        return db;
    }
    static ClaimsPrincipal User(string name, params string[] roles)
    {
        var id = new ClaimsIdentity("test");
        id.AddClaim(new Claim(ClaimTypes.Name, name));
        foreach (var r in roles) id.AddClaim(new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public async Task Resolver_maps_role_claims_to_permissions()
    {
        using var db = SeededDb();
        var perms = await new PermissionResolver(db).ResolveAsync(User("jo", "AdmissionsOfficer"));
        Assert.Contains("admissions.decide", perms);
        Assert.DoesNotContain("grades.change.approve", perms);   // not in that role
    }

    [Fact]
    public async Task Admin_has_every_permission()
    {
        using var db = SeededDb();
        var perms = await new PermissionResolver(db).ResolveAsync(User("root", "Admin"));
        Assert.Superset(Permissions.All.Select(p => p.Key).ToHashSet(), perms);
    }

    [Fact]
    public async Task Resolver_includes_db_role_assignments()
    {
        using var db = SeededDb();
        db.UserRoleAssignments.Add(new UserRoleAssignment { Username = "sam", RoleName = "Dean" });
        await db.SaveChangesAsync();
        var perms = await new PermissionResolver(db).ResolveAsync(User("sam"));   // no role claim, only DB
        Assert.Contains("grades.change.approve", perms);
    }

    [Fact]
    public async Task Handler_grants_with_permission_claim_and_denies_without()
    {
        var handler = new PermissionHandler();
        var req = new PermissionRequirement("grades.change.approve");

        var withPerm = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permission", "grades.change.approve") }, "t"));
        var ctxOk = new AuthorizationHandlerContext(new[] { req }, withPerm, null);
        await handler.HandleAsync(ctxOk);
        Assert.True(ctxOk.HasSucceeded);

        var withoutPerm = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permission", "students.read") }, "t"));
        var ctxNo = new AuthorizationHandlerContext(new[] { req }, withoutPerm, null);
        await handler.HandleAsync(ctxNo);
        Assert.False(ctxNo.HasSucceeded);
    }
}
