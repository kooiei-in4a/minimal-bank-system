using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace MinimalBankSystem.Infrastructure.Persistence.Identity;

/// <summary>
/// PostgreSQL row-lock SQL for Operator lifecycle mutations. The domain remains unaware of
/// FOR UPDATE syntax; callers must run these commands inside an active READ COMMITTED transaction.
/// </summary>
public static class OperatorMutationLocking
{
    public const string LockTimeout = "2000ms";

    public const string SetLockTimeoutSql = $"SET LOCAL lock_timeout = '{LockTimeout}'";

    public static readonly string LockActiveAdministratorsSql =
        $"""
         SELECT {OperatorPersistence.IdColumn}
         FROM {OperatorPersistence.TableName}
         WHERE {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
           AND {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}'
         ORDER BY {OperatorPersistence.IdColumn}
         FOR UPDATE
         """;

    public static readonly string LockOperatorByIdSql =
        $"""
         SELECT {OperatorPersistence.IdColumn}
         FROM {OperatorPersistence.TableName}
         WHERE {OperatorPersistence.IdColumn} = $1
         FOR UPDATE
         """;

    public static bool IsLockTimeoutOrDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState is PostgresErrorCodes.LockNotAvailable
                    or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task SetTransactionLockTimeoutAsync(
        BankDbContext persistence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        await using NpgsqlCommand command = CreateTransactionCommand(persistence, SetLockTimeoutSql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Guid>> LockActiveAdministratorIdentifiersAsync(
        BankDbContext persistence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        await using NpgsqlCommand command = CreateTransactionCommand(
            persistence,
            LockActiveAdministratorsSql);

        List<Guid> identifiers = [];
        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            identifiers.Add(reader.GetGuid(0));
        }

        return identifiers;
    }

    public static async Task<bool> TryLockOperatorByIdAsync(
        BankDbContext persistence,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        await using NpgsqlCommand command = CreateTransactionCommand(
            persistence,
            LockOperatorByIdSql);
        command.Parameters.Add(new NpgsqlParameter { Value = operatorId });

        object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is Guid lockedId && lockedId == operatorId;
    }

    private static NpgsqlCommand CreateTransactionCommand(BankDbContext persistence, string sql)
    {
        IDbContextTransaction transaction = EnsureActiveTransaction(persistence);
        DbConnection connection = persistence.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                "Operator mutation row locks require an open PostgreSQL connection.");
        }

        if (connection is not NpgsqlConnection npgsqlConnection ||
            transaction.GetDbTransaction() is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException(
                "Operator mutation row locks require a PostgreSQL connection and transaction.");
        }

        return new NpgsqlCommand(sql, npgsqlConnection, npgsqlTransaction);
    }

    private static IDbContextTransaction EnsureActiveTransaction(BankDbContext persistence)
    {
        return persistence.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Operator mutation row locks require an active caller-owned transaction.");
    }
}
