using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>
/// Application persistence baseline.
/// </summary>
/// <remarks>
/// FND-04 owns migration machinery only. Business entities belong to later schema-owning Issues,
/// so <c>InitialFoundation</c> deliberately remains empty.
/// </remarks>
/// <param name="options">Provider options built through <see cref="BankPersistence"/>.</param>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options);
