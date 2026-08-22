using System.ComponentModel.DataAnnotations;
using loans_service.Models;

namespace loans_service.Dtos;

public class UpdateLoanDto
{
    [Required]
    public required LoanStatus Status { get; set; }
}
