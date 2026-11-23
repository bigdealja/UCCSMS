using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Application.Crm;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Admissions;
using SMS.Application.Ai;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdmissionsController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly AdmissionsService _svc;
    private readonly ICrmProvider _crm;
    private readonly AdmissionsAiService _ai;
    private readonly IAiAssistant _aiAssistant;
    public AdmissionsController(SmsDbContext db, AdmissionsService svc, ICrmProvider crm, AdmissionsAiService ai, IAiAssistant aiAssistant)
    { _db = db; _svc = svc; _crm = crm; _ai = ai; _aiAssistant = aiAssistant; }

    [HttpGet("crm/health")]
    public async Task<ActionResult<object>> CrmHealth(CancellationToken ct)
        => Ok(new { provider = _crm.Name, external = _crm.IsExternal, reachable = _crm.IsExternal ? await _crm.HealthAsync(ct) : (bool?)null });

    // ---- Leads (CRM) ----
    [HttpGet("leads")]
    public async Task<ActionResult<IEnumerable<LeadDto>>> Leads([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Leads.AsNoTracking().AsQueryable();
        if (Enum.TryParse<LeadStatus>(status, true, out var st)) q = q.Where(l => l.Status == st);
        return Ok(await q.OrderByDescending(l => l.CreatedUtc).Select(l => new LeadDto(
            l.Id, l.FirstName, l.LastName, l.Email, l.Phone, l.Source, l.Status.ToString(), l.ProgrammeOfInterestId)).ToListAsync(ct));
    }

    [HttpPost("leads")]
    public async Task<ActionResult<LeadDto>> CreateLead(UpsertLeadDto dto, CancellationToken ct)
    {
        var lead = await _svc.CreateLeadAsync(new Lead {
            FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email, Phone = dto.Phone,
            Source = dto.Source ?? "Manual", ProgrammeOfInterestId = dto.ProgrammeOfInterestId }, ct);
        return Ok(new LeadDto(lead.Id, lead.FirstName, lead.LastName, lead.Email, lead.Phone, lead.Source, lead.Status.ToString(), lead.ProgrammeOfInterestId));
    }

    [HttpPost("leads/{leadId:int}/convert")]
    public async Task<ActionResult<object>> Convert(int leadId, ConvertLeadDto dto, CancellationToken ct)
    {
        try { var app = await _svc.ConvertLeadToApplicationAsync(leadId, dto.ProgrammeId, dto.IntakeTermId, ct);
              return Ok(new { app.Id, app.ApplicationNumber, app.Status }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ---- Applications ----
    static ApplicationDto Map(AdmissionApplication a) => new(a.Id, a.ApplicationNumber, a.FirstName, a.LastName, a.Email,
        a.ProgrammeId, a.IntakeTermId, a.Status.ToString(), a.Decision.ToString(), a.OfferResponse.ToString(),
        a.AssignedOfficer, a.DecidedBy, a.StudentId);

    [HttpGet("applications")]
    public async Task<ActionResult<IEnumerable<ApplicationDto>>> Applications([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Applications.AsNoTracking().AsQueryable();
        if (Enum.TryParse<ApplicationStatus>(status, true, out var st)) q = q.Where(a => a.Status == st);
        return Ok(await q.OrderByDescending(a => a.CreatedUtc).Select(a => Map(a)).ToListAsync(ct));
    }

    [HttpGet("applications/{id:int}")]
    public async Task<ActionResult<ApplicationDto>> GetApplication(int id, CancellationToken ct)
    {
        var a = await _db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? NotFound() : Ok(Map(a));
    }

    [HttpPost("applications/{id:int}/submit")]
    public Task<IActionResult> Submit(int id, CancellationToken ct) => Act(() => _svc.SubmitAsync(id, ct));

    [HttpPost("applications/{id:int}/review")]
    public Task<IActionResult> Review(int id, CancellationToken ct)
        => Act(() => _svc.StartReviewAsync(id, User.Identity?.Name ?? "officer", ct));

    [SMS.Api.Auth.HasPermission("admissions.decide")]
    [HttpPost("applications/{id:int}/decision")]
    public async Task<ActionResult<ApplicationDto>> Decision(int id, DecisionDto dto, CancellationToken ct)
    {
        try
        {
            var decider = User.Identity?.Name ?? dto.DecidedBy ?? "approver";
            if (!Enum.TryParse<AdmissionCategory>(dto.Category, true, out var cat)) return BadRequest("Invalid category.");
            var app = await _svc.RecordDecisionAsync(id, cat, decider, dto.Notes, ct);
            return Ok(Map(app));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("applications/{id:int}/offer-response")]
    public async Task<IActionResult> OfferResponse(int id, OfferResponseDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<OfferResponse>(dto.Response, true, out var r)) return BadRequest("Invalid response.");
        await _svc.RecordOfferResponseAsync(id, r, ct);
        return NoContent();
    }

    [HttpPost("applications/{id:int}/convert-to-student")]
    public async Task<ActionResult<object>> ConvertToStudent(int id, ConvertToStudentDto dto, CancellationToken ct)
    {
        try { var s = await _svc.ConvertToStudentAsync(id, dto.StudentNumber, ct);
              return Ok(new { studentId = s.Id, s.StudentNumber, s.Status }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("ai/health")]
    public ActionResult<object> AiHealth()
        => Ok(new { provider = _aiAssistant.Name, configured = _aiAssistant.IsConfigured });

    /// <summary>Produce an ADVISORY admission recommendation (AI if enabled, else rules).
    /// A human still approves via /decision (second-person). Never auto-decides.</summary>
    [HttpPost("applications/{id:int}/ai-recommend")]
    public async Task<ActionResult<object>> Recommend(int id, CancellationToken ct)
    {
        try
        {
            var r = await _ai.RecommendAsync(id, ct);
            return Ok(new { r.RecommendedCategory, r.Confidence, r.Rationale, r.Source, r.MissingDocuments });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("applications/{id:int}/recommendations")]
    public async Task<ActionResult<object>> Recommendations(int id, CancellationToken ct)
        => Ok(await _db.AdmissionRecommendations.AsNoTracking().Where(x => x.ApplicationId == id)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new { x.RecommendedCategory, x.Confidence, x.Rationale, x.Source, x.CreatedUtc }).ToListAsync(ct));

    async Task<IActionResult> Act(Func<Task> f)
    {
        try { await f(); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
