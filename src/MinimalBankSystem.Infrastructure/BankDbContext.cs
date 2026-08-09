using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure;

public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
}
