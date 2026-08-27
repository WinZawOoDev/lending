namespace loans_service.Models;

public static class LoanStatusRules
{
    private static readonly Dictionary<LoanStatus, LoanStatus[]> AllowedTransitions = new()
    {
        [LoanStatus.Pending] = new[] { LoanStatus.Pending, LoanStatus.Active, LoanStatus.Defaulted },
        [LoanStatus.Active] = new[] { LoanStatus.Active, LoanStatus.Paid, LoanStatus.Defaulted },
        [LoanStatus.Paid] = new[] { LoanStatus.Paid },
        [LoanStatus.Defaulted] = new[] { LoanStatus.Defaulted },
    };

    public static bool CanTransition(LoanStatus current, LoanStatus next)
        => AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
}