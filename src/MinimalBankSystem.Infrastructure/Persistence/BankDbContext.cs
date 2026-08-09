using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Application persistence baseline. FND-04 owns migration machinery only;
/// business entities remain with future schema-owning Issues.
/// </summary>
public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Intentionally empty: no Customer / Account / Operator / Identity /
        // AuditLog / Transaction / Idempotency (or other business) tables here.
        base.OnModelCreating(modelBuilder);
    }
}
