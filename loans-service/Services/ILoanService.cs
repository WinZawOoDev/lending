using loans_service.Dtos;
using loans_service.Models;

namespace loans_service.Services;

public interface ILoanService
{
    Task<List<Loan>> GetAllAsync(CancellationToken cancellationToken);

    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Loan> CreateAsync(CreateLoanDto dto, CancellationToken cancellationToken);

    Task<Loan?> UpdateAsync(Guid id, UpdateLoanDto dto, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
