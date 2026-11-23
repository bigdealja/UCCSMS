using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Infrastructure.Pdf;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PdfController : ControllerBase
{
    private readonly PdfService _pdf;
    public PdfController(PdfService pdf) => _pdf = pdf;

    [HttpGet("transcript/{studentId:int}")]
    public async Task<IActionResult> Transcript(int studentId, CancellationToken ct)
    {
        try { return File(await _pdf.TranscriptAsync(studentId, ct), "application/pdf", $"transcript-{studentId}.pdf"); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("acceptance-letter/{applicationId:int}")]
    public async Task<IActionResult> AcceptanceLetter(int applicationId, CancellationToken ct)
    {
        try { return File(await _pdf.AcceptanceLetterAsync(applicationId, ct), "application/pdf", $"acceptance-{applicationId}.pdf"); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}
