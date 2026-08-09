using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure;

public static class BankDbContextOptions
{
    public static DbContextOptions<BankDbContext> Create(
        string? connectionString,
        TimeSpan? commandTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbContextOptionsBuilder<BankDbContext> builder = new();
        Configure(builder, connectionString, commandTimeout);
        return builder.Options;
    }

    public static void Configure(
        DbContextOptionsBuilder optionsBuilder,
        string? connectionString,
        TimeSpan? commandTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A PostgreSQL connection string is required to configure BankDbContext.",
                nameof(connectionString));
        }

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                if (commandTimeout is not null)
                {
                    npgsqlOptions.CommandTimeout((int)commandTimeout.Value.TotalSeconds);
                }
            });
    }
}
