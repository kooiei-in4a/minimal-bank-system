using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;

string connectionString;

try
{
    connectionString = BankDbContextConnectionStringResolver.Resolve();
}
catch (InvalidOperationException exception)
{
    await Console.Error.WriteLineAsync("Migrator startup failed.");
    await Console.Error.WriteLineAsync(exception.ToString());
    return 1;
}

DbContextOptions options = new DbContextOptionsBuilder()
    .UseBankPostgreSql(connectionString)
    .Options;

await using BankDbContext dbContext = new(options);

return await MigrationRunner.RunAsync(
    token => dbContext.Database.MigrateAsync(token),
    Console.Out,
    Console.Error);
