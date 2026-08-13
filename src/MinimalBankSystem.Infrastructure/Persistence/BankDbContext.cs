using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Application persistence context shared by the API, explicit migrator and integration tests.
/// </summary>
/// <param name="options">Provider options built through <see cref="BankPersistence"/>.</param>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    /// <summary>The persisted Operator identities owned by WP2-ID-01.</summary>
    public DbSet<Operator> Operators => Set<Operator>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OperatorConfiguration());
    }
}
