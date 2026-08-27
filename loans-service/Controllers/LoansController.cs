using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using loans_service.Dtos;
using loans_service.Models;
using loans_service.Services;

namespace loans_service.Controllers;

[ApiController]
[Authorize]
[Route("loans")]
public class LoansController(ILoanService loanService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<Loan>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] LoanStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await loanService.GetAllAsync(page, pageSize, status, cancellationToken);
        return Ok(result);
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
        var result = await loanService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            LoanUpdateStatus.NotFound => NotFound(),
            LoanUpdateStatus.InvalidTransition => Conflict(
                new { message = $"Invalid status transition from '{result.Loan!.Status}' to '{dto.Status}'." }),
            _ => Ok(result.Loan),
        };
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