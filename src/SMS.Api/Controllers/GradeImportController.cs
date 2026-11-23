using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Application.GradeImport;
using SMS.Infrastructure.GradeImport;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradeImportController : ControllerBase
{
    private readonly IGradeFileParser _parser;
    private readonly GradeImportService _service;
    public GradeImportController(IGradeFileParser parser, GradeImportService service)
    { _parser = parser; _service = service; }

    /// <summary>Step 1: inspect a file's column headers so the UI can build the mapping.</summary>
    [HttpPost("columns")]
    [RequestSizeLimit(20_000_000)]
    public ActionResult<IEnumerable<string>> Columns(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        using var s = file.OpenReadStream();
        var sheet = _parser.Parse(s, file.FileName);
        return Ok(sheet.Headers);
    }

    /// <summary>Step 2: validate rows against the DB without writing anything.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ImportPreview>> Preview([FromForm] GradeImportForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0) return BadRequest("No file uploaded.");
        using var s = form.File.OpenReadStream();
        var sheet = _parser.Parse(s, form.File.FileName);
        var preview = await _service.PreviewAsync(form.TermId, form.CourseId, sheet, form.ToMapping(), ct);
        return Ok(preview);
    }

    /// <summary>Step 3: re-validate server-side and write all non-error rows as one undoable batch.</summary>
    [HttpPost("commit")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<CommitResult>> Commit([FromForm] GradeImportForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0) return BadRequest("No file uploaded.");
        using var s = form.File.OpenReadStream();
        var sheet = _parser.Parse(s, form.File.FileName);
        var preview = await _service.PreviewAsync(form.TermId, form.CourseId, sheet, form.ToMapping(), ct);
        var user = User.Identity?.Name ?? "system";
        var result = await _service.CommitAsync(form.TermId, form.CourseId, preview, user, ct);
        return Ok(result);
    }
}

public class GradeImportForm
{
    public IFormFile File { get; set; } = default!;
    public int TermId { get; set; }
    public int CourseId { get; set; }
    public string StudentNumberColumn { get; set; } = default!;
    public string? LetterGradeColumn { get; set; }
    public string? CourseworkColumn { get; set; }
    public string? FinalExamColumn { get; set; }
    public GradeImportMapping ToMapping() =>
        new(StudentNumberColumn, LetterGradeColumn, CourseworkColumn, FinalExamColumn);
}
