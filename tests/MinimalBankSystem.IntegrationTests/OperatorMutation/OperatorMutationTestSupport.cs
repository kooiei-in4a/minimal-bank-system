extern alias api;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorMutation;

internal static class OperatorMutationTestSupport
{
    public const string SeedPlaintextPassword = "OperatorMutation-Seed-Password-Only-123!";

    public static readonly DateTimeOffset FrozenUtcNow =
        new(2034, 4, 5, 6, 7, 8, TimeSpan.Zero);

    public static async Task MigrateAsync(string connectionString)
    {
        MigratorRun run = await MigratorProcess.RunAsync(connectionString, TimeSpan.FromSeconds(120));
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected Operator mutation migration success. Output:{Environment.NewLine}{run.Output}");
    }

    public static BankDbContext CreateContext(string connectionString)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(connectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    public static async Task<Operator> SeedOperatorAsync(
        string connectionString,
        string userName,
        OperatorRole role,
        DateTimeOffset? createdAt = null)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            createdAt ?? FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using BankDbContext context = CreateContext(connectionString);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    public static async Task DeleteAllOperatorsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"DELETE FROM {OperatorPersistence.TableName};",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<Operator> SeedDisabledOperatorAsync(
        string connectionString,
        string userName,
        OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        created.Disable(FrozenUtcNow.AddMinutes(1), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using BankDbContext context = CreateContext(connectionString);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    public static async Task<Operator> ReadOperatorAsync(string connectionString, Guid identifier)
    {
        await using BankDbContext context = CreateContext(connectionString);
        return await context.Operators.AsNoTracking().SingleAsync(candidate => candidate.Id == identifier);
    }

    public static async Task<long> CountActiveAdministratorsAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*)
             FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
               AND {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}';
             """,
            connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public static async Task<IReadOnlyList<PersistedOperatorMutationAudit>> ReadAuditsAsync(
        string connectionString,
        string correlationId)
    {
        List<PersistedOperatorMutationAudit> audits = [];
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT
                 {AuditPersistence.ActorIdentifierColumn},
                 {AuditPersistence.ActorRoleColumn},
                 {AuditPersistence.OperationIdentifierColumn},
                 {AuditPersistence.TargetIdentifierColumn},
                 {AuditPersistence.ResultColumn},
                 {AuditPersistence.FailureBusinessErrorCodeColumn},
                 {AuditPersistence.CorrelationIdColumn}
             FROM {AuditPersistence.TableName}
             WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;
             """,
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            audits.Add(new PersistedOperatorMutationAudit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6)));
        }

        return audits;
    }

    public static async Task<long> CountAuditsAsync(string connectionString, string? correlationId = null)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        string sql = correlationId is null
            ? $"SELECT count(*) FROM {AuditPersistence.TableName};"
            : $"""
               SELECT count(*) FROM {AuditPersistence.TableName}
               WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;
               """;
        await using NpgsqlCommand command = new(sql, connection);
        if (correlationId is not null)
        {
            command.Parameters.AddWithValue("correlation_id", correlationId);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public static async Task<HttpResponseMessage> SendEnableAsync(
        HttpClient client,
        string token,
        Guid operatorIdentifier,
        string correlationId) =>
        await SendMutationAsync(client, token, $"/operators/{operatorIdentifier:D}/enable", correlationId, content: null);

    public static async Task<HttpResponseMessage> SendDisableAsync(
        HttpClient client,
        string token,
        Guid operatorIdentifier,
        string correlationId) =>
        await SendMutationAsync(client, token, $"/operators/{operatorIdentifier:D}/disable", correlationId, content: null);

    public static async Task<HttpResponseMessage> SendRoleChangeAsync(
        HttpClient client,
        string token,
        Guid operatorIdentifier,
        string correlationId,
        string? role)
    {
        HttpContent? content = role is null
            ? new StringContent("{}", Encoding.UTF8, "application/json")
            : JsonContent.Create(new { role });
        return await SendMutationAsync(
            client,
            token,
            $"/operators/{operatorIdentifier:D}/role",
            correlationId,
            content);
    }

    public static async Task<HttpResponseMessage> SendRawRoleChangeAsync(
        HttpClient client,
        string token,
        Guid operatorIdentifier,
        string correlationId,
        string? body,
        string mediaType = "application/json")
    {
        HttpContent? content = body is null ? null : new StringContent(body, Encoding.UTF8, mediaType);
        return await SendMutationAsync(
            client,
            token,
            $"/operators/{operatorIdentifier:D}/role",
            correlationId,
            content);
    }

    public static async Task<HttpResponseMessage> SendMutationAsync(
        HttpClient client,
        string? token,
        string path,
        string correlationId,
        HttpContent? content)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = content;
        return await client.SendAsync(request);
    }

    public static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}. Body: {body}");
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    public static void AssertExactProjection(JsonElement projection, Guid operatorIdentifier, string state, string role)
    {
        Assert.Equal(operatorIdentifier, projection.GetProperty("operatorIdentifier").GetGuid());
        Assert.Equal(state, projection.GetProperty("state").GetString());
        Assert.Equal(role, projection.GetProperty("role").GetString());

        string[] approvedFields = ["operatorIdentifier", "state", "role"];
        Assert.Equal(approvedFields.Length, projection.EnumerateObject().Count());
        foreach (JsonProperty property in projection.EnumerateObject())
        {
            Assert.Contains(property.Name, approvedFields);
        }
    }

    public static string CreateToken(Operator operatorEntity, int? authorizationStateVersion = null)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    (authorizationStateVersion ?? operatorEntity.AuthorizationStateVersion)
                    .ToString(CultureInfo.InvariantCulture)),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed record PersistedOperatorMutationAudit(
    Guid ActorIdentifier,
    string ActorRole,
    string OperationIdentifier,
    string TargetIdentifier,
    string Result,
    string? FailureBusinessErrorCode,
    string CorrelationId);

internal sealed class OperatorMutationApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);
        builder.ConfigureServices(services => configureServices?.Invoke(services));
    }
}

internal sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
