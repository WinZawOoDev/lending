using Microsoft.EntityFrameworkCore;
using loans_service.Models;

namespace loans_service.Data;

public class LoansDbContext(DbContextOptions<LoansDbContext> options) : DbContext(options)
{
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("loans");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(l => l.AccountId).HasColumnName("account_id").HasMaxLength(36);
            entity.Property(l => l.Principal).HasColumnName("principal").HasColumnType("numeric(12,2)");
            entity.Property(l => l.InterestRate).HasColumnName("interest_rate").HasColumnType("numeric(5,2)");
            entity.Property(l => l.TermMonths).HasColumnName("term_months");
            entity.Property(l => l.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            entity.Property(l => l.CreatedAt).HasColumnName("created_at");
            entity.Property(l => l.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(l => l.AccountId);
        });
    }
}
