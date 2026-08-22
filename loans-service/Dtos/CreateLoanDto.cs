using System.ComponentModel.DataAnnotations;

namespace loans_service.Dtos;

public class CreateLoanDto
{
    [Required]
    [MaxLength(36)]
    public required string AccountId { get; set; }

    [Range(0.01, 99_999_999)]
    public decimal Principal { get; set; }

    [Range(0, 100)]
    public decimal InterestRate { get; set; }

    [Range(1, 360)]
    public int TermMonths { get; set; }
}
