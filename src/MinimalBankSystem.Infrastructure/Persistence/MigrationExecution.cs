using Microsoft.EntityFrameworkCore;

namespace MinimalBankSystem.Infrastructure.Persistence;

public static class MigrationExecution
{
    public const int CommandTimeoutSeconds = 60;

    public static readonly TimeSpan CancellationBudget = TimeSpan.FromSeconds(CommandTimeoutSeconds);

    public static Task MigrateAsync(BankDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Database.MigrateAsync(cancellationToken);
    }
}
