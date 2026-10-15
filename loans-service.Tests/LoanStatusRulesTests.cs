using loans_service.Models;
using Xunit;

namespace loans_service.Tests;

public class LoanStatusRulesTests
{
    [Theory]
    [InlineData(LoanStatus.Pending, LoanStatus.Pending)]
    [InlineData(LoanStatus.Pending, LoanStatus.Active)]
    [InlineData(LoanStatus.Pending, LoanStatus.Defaulted)]
    [InlineData(LoanStatus.Active, LoanStatus.Active)]
    [InlineData(LoanStatus.Active, LoanStatus.Paid)]
    [InlineData(LoanStatus.Active, LoanStatus.Defaulted)]
    [InlineData(LoanStatus.Paid, LoanStatus.Paid)]
    [InlineData(LoanStatus.Defaulted, LoanStatus.Defaulted)]
    public void CanTransition_AllowsKnownTransitions(LoanStatus current, LoanStatus next)
    {
        Assert.True(LoanStatusRules.CanTransition(current, next));
    }

    [Theory]
    [InlineData(LoanStatus.Pending, LoanStatus.Paid)]
    [InlineData(LoanStatus.Paid, LoanStatus.Active)]
    [InlineData(LoanStatus.Paid, LoanStatus.Defaulted)]
    [InlineData(LoanStatus.Paid, LoanStatus.Pending)]
    [InlineData(LoanStatus.Defaulted, LoanStatus.Active)]
    [InlineData(LoanStatus.Defaulted, LoanStatus.Paid)]
    [InlineData(LoanStatus.Defaulted, LoanStatus.Pending)]
    public void CanTransition_RejectsInvalidTransitions(LoanStatus current, LoanStatus next)
    {
        Assert.False(LoanStatusRules.CanTransition(current, next));
    }
}
