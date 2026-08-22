using Microsoft.EntityFrameworkCore;

namespace loans_service.Data;

public class LoansDbContext(DbContextOptions<LoansDbContext> options) : DbContext(options)
{
}
