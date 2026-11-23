using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Enums;
using SMS.Infrastructure.Documents;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly DocumentService _docs;
    public DocumentsController(SmsDbContext db, DocumentService docs) { _db = db; _docs = docs; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> List(
        [FromQuery] int? studentId, [FromQuery] string? category, CancellationToken ct)
    {
        var q = _db.Documents.AsNoTracking().AsQueryable();
        if (studentId is int sid) q = q.Where(d => d.StudentId == sid);
        if (Enum.TryParse<DocumentCategory>(category, true, out var cat)) q = q.Where(d => d.Category == cat);
        return Ok(await q.OrderByDescending(d => d.CreatedUtc).Select(d => new DocumentDto(
            d.Id, d.Title, d.Category.ToString(), d.StudentId, d.Description, d.CurrentVersion)).ToListAsync(ct));
    }

    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadDocumentForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0) return BadRequest("No file uploaded.");
        var cat = Enum.TryParse<DocumentCategory>(form.Category, true, out var c) ? c : DocumentCategory.Other;
        await using var s = form.File.OpenReadStream();
        var doc = await _docs.UploadNewAsync(form.Title, cat, form.StudentId, form.Description,
            form.File.FileName, form.File.ContentType ?? "application/octet-stream", s,
            User.Identity?.Name, ct);
        return Ok(new DocumentDto(doc.Id, doc.Title, doc.Category.ToString(), doc.StudentId, doc.Description, doc.CurrentVersion));
    }

    [HttpPost("{id:int}/versions")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<object>> AddVersion(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        await using var s = file.OpenReadStream();
        try
        {
            var v = await _docs.AddVersionAsync(id, file.FileName, file.ContentType ?? "application/octet-stream", s, User.Identity?.Name, ct);
            return Ok(new { v.VersionNumber, v.SizeBytes, v.Sha256 });
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, [FromQuery] int? version, CancellationToken ct)
    {
        try
        {
            var (meta, content) = await _docs.OpenAsync(id, version, ct);
            return File(content, meta.ContentType, meta.FileName);
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _docs.DeleteAsync(id, ct);
        return NoContent();
    }
}

public class UploadDocumentForm
{
    public IFormFile File { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Category { get; set; }
    public int? StudentId { get; set; }
    public string? Description { get; set; }
}
