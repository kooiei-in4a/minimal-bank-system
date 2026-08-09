using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class BankDbContext(DbContextOptions options) : DbContext(options);
