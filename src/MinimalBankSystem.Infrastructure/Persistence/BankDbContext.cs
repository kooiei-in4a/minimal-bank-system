using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options);
