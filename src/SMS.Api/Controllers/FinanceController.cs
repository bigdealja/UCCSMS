using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Application.Accounting;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Finance;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly FinanceService _finance;
    private readonly IAccountingProvider _accounting;
    public FinanceController(SmsDbContext db, FinanceService finance, IAccountingProvider accounting)
    { _db = db; _finance = finance; _accounting = accounting; }

    /// <summary>Which accounting system is wired in, and is it reachable.</summary>
    [HttpGet("accounting/health")]
    public async Task<ActionResult<object>> AccountingHealth(CancellationToken ct)
        => Ok(new { provider = _accounting.Name, external = _accounting.IsExternal,
                    reachable = _accounting.IsExternal ? await _accounting.HealthAsync(ct) : (bool?)null });

    [HttpGet("fee-types")]
    public async Task<ActionResult<IEnumerable<FeeTypeDto>>> FeeTypes(CancellationToken ct) =>
        Ok(await _db.FeeTypes.AsNoTracking().Where(f => f.IsActive).OrderBy(f => f.Name)
            .Select(f => new FeeTypeDto(f.Id, f.Code, f.Name, f.DefaultAmount, f.DistributionCode)).ToListAsync(ct));

    [HttpPost("fee-types")]
    public async Task<ActionResult<FeeTypeDto>> CreateFeeType(UpsertFeeTypeDto dto, CancellationToken ct)
    {
        var f = new FeeType { Code = dto.Code, Name = dto.Name, DefaultAmount = dto.DefaultAmount, DistributionCode = dto.DistributionCode };
        _db.FeeTypes.Add(f);
        await _db.SaveChangesAsync(ct);
        return Ok(new FeeTypeDto(f.Id, f.Code, f.Name, f.DefaultAmount, f.DistributionCode));
    }

    [HttpGet("students/{studentId:int}/statement")]
    public async Task<ActionResult<Statement>> Statement(int studentId, CancellationToken ct)
        => Ok(await _finance.GetStatementAsync(studentId, ct));

    [SMS.Api.Auth.HasPermission("finance.post")]
    [HttpPost("students/{studentId:int}/charges")]
    public async Task<ActionResult<object>> Charge(int studentId, ChargeDto dto, CancellationToken ct)
    {
        try
        {
            var tx = await _finance.PostChargeAsync(studentId, dto.TermId, dto.FeeTypeId, dto.Amount, dto.Description, ct);
            return Ok(new { tx.Id, tx.Amount, accounting = tx.AccountingStatus.ToString(), tx.AccountingRef, tx.AccountingError });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [SMS.Api.Auth.HasPermission("finance.post")]
    [HttpPost("students/{studentId:int}/payments")]
    public async Task<ActionResult<object>> Payment(int studentId, PaymentDto dto, CancellationToken ct)
    {
        try
        {
            var method = Enum.TryParse<PaymentMethod>(dto.Method, true, out var m) ? m : PaymentMethod.Other;
            var tx = await _finance.RecordPaymentAsync(studentId, dto.Amount, method, dto.Reference, dto.Description, dto.IsRefund, ct);
            return Ok(new { tx.Id, tx.Amount, accounting = tx.AccountingStatus.ToString(), tx.AccountingRef, tx.AccountingError });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
