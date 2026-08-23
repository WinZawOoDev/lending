using Microsoft.EntityFrameworkCore;
using loans_service.Data;
using loans_service.Dtos;
using loans_service.Middleware;
using loans_service.Models;

namespace loans_service.Services;

public class LoanService(LoansDbContext context, ILogger<LoanService> logger) : ILoanService
{
    public async Task<List<Loan>> GetAllAsync(CancellationToken cancellationToken)
    {
        var loans = await context.Loans
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Retrieved {Count} loans (correlation {CorrelationId})",
            loans.Count, CorrelationContext.CorrelationId);

        return loans;
    }

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);

        if (loan is null)
        {
            logger.LogWarning("Loan {LoanId} not found (correlation {CorrelationId})", id, CorrelationContext.CorrelationId);
        }

        return loan;
    }

    public async Task<Loan> CreateAsync(CreateLoanDto dto, CancellationToken cancellationToken)
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

        logger.LogInformation(
            "Created loan {LoanId} for account {AccountId} with principal {Principal} (correlation {CorrelationId})",
            loan.Id, loan.AccountId, loan.Principal, CorrelationContext.CorrelationId);

        return loan;
    }

    public async Task<Loan?> UpdateAsync(Guid id, UpdateLoanDto dto, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            logger.LogWarning("Cannot update missing loan {LoanId} (correlation {CorrelationId})", id, CorrelationContext.CorrelationId);
            return null;
        }

        loan.Status = dto.Status;
        loan.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated loan {LoanId} status to {Status} (correlation {CorrelationId})",
            loan.Id, loan.Status, CorrelationContext.CorrelationId);

        return loan;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            logger.LogWarning("Cannot delete missing loan {LoanId} (correlation {CorrelationId})", id, CorrelationContext.CorrelationId);
            return false;
        }

        context.Loans.Remove(loan);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Deleted loan {LoanId} for account {AccountId} (correlation {CorrelationId})",
            loan.Id, loan.AccountId, CorrelationContext.CorrelationId);

        return true;
    }
}
