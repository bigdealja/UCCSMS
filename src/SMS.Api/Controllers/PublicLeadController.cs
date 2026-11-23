using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SMS.Api.Dtos;
using SMS.Application.Crm;
using SMS.Domain.Entities;
using SMS.Infrastructure.Admissions;

namespace SMS.Api.Controllers;

/// <summary>Public, unauthenticated lead capture for the institution's website — the in-house
/// replacement for Mautic's web capture. The site POSTs a prospective student here and the lead
/// lands directly in the built-in CRM. Protected by an optional shared secret to deter spam.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicLeadController : ControllerBase
{
    private readonly AdmissionsService _svc;
    private readonly CrmOptions _crm;
    public PublicLeadController(AdmissionsService svc, IOptions<CrmOptions> crm) { _svc = svc; _crm = crm.Value; }

    [HttpPost("lead")]
    public async Task<ActionResult<object>> Capture([FromBody] PublicLeadDto dto,
        [FromHeader(Name = "X-Capture-Secret")] string? secret, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_crm.CaptureSecret) && _crm.CaptureSecret != secret)
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest("First and last name are required.");

        var lead = await _svc.CreateLeadAsync(new Lead {
            FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email, Phone = dto.Phone,
            Source = dto.Source ?? "Website", ProgrammeOfInterestId = dto.ProgrammeOfInterestId }, ct);
        return Ok(new { received = true, leadId = lead.Id });
    }
}
