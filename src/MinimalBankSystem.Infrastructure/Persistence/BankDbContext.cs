using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Single application DbContext. WP2-ID-01 adds Operator identity persistence to the same
/// EF migration history established by FND-04.
/// </summary>
/// <param name="options">Provider options built through <see cref="BankPersistence"/>.</param>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    public DbSet<Operator> Operators => Set<Operator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);
    }
}
