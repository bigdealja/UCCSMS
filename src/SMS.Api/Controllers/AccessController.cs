using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccessController : ControllerBase
{
    private readonly SmsDbContext _db;
    public AccessController(SmsDbContext db) => _db = db;

    /// <summary>The caller's own identity, roles and effective permissions (drives UI gating).</summary>
    [HttpGet("me")]
    public ActionResult<object> Me() => Ok(new
    {
        user = User.Identity?.Name,
        roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)
            .Concat(User.FindAll("role").Select(c => c.Value)).Distinct(),
        permissions = User.FindAll("permission").Select(c => c.Value).OrderBy(x => x)
    });

    [HttpGet("permissions")]
    [HasPermission("admin.manage")]
    public async Task<ActionResult<object>> PermissionCatalog(CancellationToken ct)
        => Ok(await _db.Permissions.AsNoTracking().OrderBy(p => p.Key).Select(p => new { p.Key, p.Description }).ToListAsync(ct));

    [HttpGet("roles")]
    [HasPermission("admin.manage")]
    public async Task<ActionResult<object>> Roles(CancellationToken ct)
        => Ok(await _db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new { r.Name, r.Description, permissions = r.Permissions.Select(p => p.Permission.Key) }).ToListAsync(ct));

    [HttpGet("assignments")]
    [HasPermission("admin.manage")]
    public async Task<ActionResult<IEnumerable<UserRoleDto>>> Assignments(CancellationToken ct)
        => Ok(await _db.UserRoleAssignments.AsNoTracking().OrderBy(a => a.Username)
            .Select(a => new UserRoleDto(a.Id, a.Username, a.RoleName, a.CampusId)).ToListAsync(ct));

    [HttpPost("assignments")]
    [HasPermission("admin.manage")]
    public async Task<ActionResult<UserRoleDto>> Assign(UpsertUserRoleDto dto, CancellationToken ct)
    {
        if (!await _db.Roles.AnyAsync(r => r.Name == dto.RoleName, ct)) return BadRequest("Unknown role.");
        if (await _db.UserRoleAssignments.AnyAsync(a => a.Username == dto.Username && a.RoleName == dto.RoleName, ct))
            return Conflict("Already assigned.");
        var a = new UserRoleAssignment { Username = dto.Username, RoleName = dto.RoleName, CampusId = dto.CampusId };
        _db.UserRoleAssignments.Add(a); await _db.SaveChangesAsync(ct);
        return Ok(new UserRoleDto(a.Id, a.Username, a.RoleName, a.CampusId));
    }

    [HttpDelete("assignments/{id:int}")]
    [HasPermission("admin.manage")]
    public async Task<IActionResult> Unassign(int id, CancellationToken ct)
    {
        var a = await _db.UserRoleAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        _db.UserRoleAssignments.Remove(a); await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
