using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Application persistence baseline.
/// </summary>
/// <remarks>
/// FND-04 owns the migration machinery only. Customer, Account, Operator, Identity, AuditLog,
/// Transaction and Idempotency entities are owned by later schema-owning Issues and are
/// deliberately absent here, so <c>InitialFoundation</c> stays an empty baseline migration.
/// </remarks>
/// <param name="options">Provider options built through <see cref="BankPersistence"/>.</param>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options);
