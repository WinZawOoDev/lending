using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using loans_service.Data;
using loans_service.Dtos;
using loans_service.Models;

namespace loans_service.Controllers;

[ApiController]
[Route("loans")]
public class LoansController(LoansDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Loan>>> GetAll(CancellationToken cancellationToken)
    {
        var loans = await context.Loans
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(loans);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Loan>> GetOne(Guid id, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }
        return Ok(loan);
    }

    [HttpPost]
    public async Task<ActionResult<Loan>> Create(
        CreateLoanDto dto,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var loan = new Loan
        {
            AccountId = dto.AccountId,
            Principal = dto.Principal,
            InterestRate = dto.InterestRate,
            TermMonths = dto.TermMonths,
            Status = LoanStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.Loans.Add(loan);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetOne), new { id = loan.Id }, loan);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Loan>> Update(
        Guid id,
        UpdateLoanDto dto,
        CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        loan.Status = dto.Status;
        loan.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return Ok(loan);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        context.Loans.Remove(loan);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
