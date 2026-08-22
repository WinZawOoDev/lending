using System.ComponentModel.DataAnnotations;

namespace loans_service.Models;

public class Loan
{
    public Guid Id { get; set; }

    [Required]
    public required string AccountId { get; set; }

    public decimal Principal { get; set; }

    public decimal InterestRate { get; set; }

    public int TermMonths { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
