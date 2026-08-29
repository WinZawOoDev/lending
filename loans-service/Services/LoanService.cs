using Microsoft.EntityFrameworkCore;
using loans_service.Audit;
using loans_service.Data;
using loans_service.Dtos;
using loans_service.Middleware;
using loans_service.Models;

namespace loans_service.Services;

public class LoanService(
    LoansDbContext context,
    ILogger<LoanService> logger,
    AuditService auditService) : ILoanService
{
    public async Task<PagedResult<Loan>> GetAllAsync(
        int page,
        int pageSize,
        LoanStatus? status,
        CancellationToken cancellationToken)
    {
        var query = context.Loans.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(l => l.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var loans = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Retrieved {Count} loans for page {Page} (correlation {CorrelationId})",
            loans.Count, page, CorrelationContext.CorrelationId);

        return new PagedResult<Loan>(loans, totalCount, page, pageSize);
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

        await auditService.RecordAsync(
            "loan.created",
            "loan",
            loan.Id.ToString(),
            null,
            loan,
            cancellationToken);

        return loan;
    }

    public async Task<LoanUpdateResult> UpdateAsync(Guid id, UpdateLoanDto dto, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            logger.LogWarning("Cannot update missing loan {LoanId} (correlation {CorrelationId})", id, CorrelationContext.CorrelationId);
            return new LoanUpdateResult(LoanUpdateStatus.NotFound);
        }

        if (!LoanStatusRules.CanTransition(loan.Status, dto.Status))
        {
            logger.LogWarning(
                "Invalid status transition for loan {LoanId}: {Current} -> {Next} (correlation {CorrelationId})",
                loan.Id, loan.Status, dto.Status, CorrelationContext.CorrelationId);
            return new LoanUpdateResult(LoanUpdateStatus.InvalidTransition, loan);
        }

        var before = new { loan.Status, loan.UpdatedAt };

        loan.Status = dto.Status;
        loan.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated loan {LoanId} status to {Status} (correlation {CorrelationId})",
            loan.Id, loan.Status, CorrelationContext.CorrelationId);

        await auditService.RecordAsync(
            "loan.updated",
            "loan",
            loan.Id.ToString(),
            before,
            loan,
            cancellationToken);

        return new LoanUpdateResult(LoanUpdateStatus.Updated, loan);
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

        await auditService.RecordAsync(
            "loan.deleted",
            "loan",
            loan.Id.ToString(),
            loan,
            null,
            cancellationToken);

        return true;
    }
}