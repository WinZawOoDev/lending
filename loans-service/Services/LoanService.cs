using Microsoft.EntityFrameworkCore;
using loans_service.Data;
using loans_service.Dtos;
using loans_service.Models;

namespace loans_service.Services;

public class LoanService(LoansDbContext context) : ILoanService
{
    public async Task<List<Loan>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Loans
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Loans.FindAsync([id], cancellationToken);
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

        return loan;
    }

    public async Task<Loan?> UpdateAsync(Guid id, UpdateLoanDto dto, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return null;
        }

        loan.Status = dto.Status;
        loan.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return loan;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await context.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return false;
        }

        context.Loans.Remove(loan);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
