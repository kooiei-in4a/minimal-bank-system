using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Application persistence baseline.
/// </summary>
/// <remarks>
/// FND-04 established migration machinery over an intentionally empty <c>InitialFoundation</c>
/// baseline. WP2-ID-01 is the first schema-owning leaf and adds Operator identity persistence.
/// This remains the single <see cref="DbContext"/> and single EF migration history for the whole
/// application; later schema-owning leaves extend this same context rather than introducing one
/// of their own.
/// </remarks>
/// <param name="options">Provider options built through <see cref="BankPersistence"/>.</param>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    public DbSet<Operator> Operators => Set<Operator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OperatorConfiguration());
    }
}
