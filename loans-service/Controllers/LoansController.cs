using Microsoft.AspNetCore.Mvc;
using loans_service.Dtos;
using loans_service.Models;
using loans_service.Services;

namespace loans_service.Controllers;

[ApiController]
[Route("loans")]
public class LoansController(ILoanService loanService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Loan>>> GetAll(CancellationToken cancellationToken)
    {
        var loans = await loanService.GetAllAsync(cancellationToken);
        return Ok(loans);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Loan>> GetOne(Guid id, CancellationToken cancellationToken)
    {
        var loan = await loanService.GetByIdAsync(id, cancellationToken);
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
        var loan = await loanService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(GetOne), new { id = loan.Id }, loan);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Loan>> Update(
        Guid id,
        UpdateLoanDto dto,
        CancellationToken cancellationToken)
    {
        var loan = await loanService.UpdateAsync(id, dto, cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        return Ok(loan);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await loanService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
