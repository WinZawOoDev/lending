using loans_service.Models;

namespace loans_service.Services;

public enum LoanUpdateStatus
{
    Updated,
    NotFound,
    InvalidTransition,
}

public sealed record LoanUpdateResult(LoanUpdateStatus Status, Loan? Loan = null);