using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
}
