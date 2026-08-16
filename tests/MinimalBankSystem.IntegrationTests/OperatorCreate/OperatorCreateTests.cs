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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorCreate;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.IntegrationTests.PostgreSql;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

/// <summary>
/// Issue #170 (WP2-OPR-CREATE-01) verification: the approved create contract, duplicate/invalid
/// rejection, Audit ownership and atomicity, and credential non-disclosure with a positive
/// control, all through the real ASP.NET Core pipeline and a real PostgreSQL database.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorCreateTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtcNow =
        new(2031, 9, 10, 11, 12, 13, TimeSpan.Zero);

    private const string SeedPlaintextPassword = "OperatorCreate-Seed-Password-Only-123!";

    [Theory]
    [InlineData("administrator")]
    [InlineData("teller")]
    [InlineData("viewer")]
    public async Task AdministratorCreateSucceedsAndReturnsExactProjection(string roleToken)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.success.admin", OperatorRole.Administrator);
        string loginIdentifier = $"opr.create.success.{roleToken}";
        const string plaintextPassword = "Brand-New-Operator-Password-456!";

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-success-" + roleToken,
            loginIdentifier,
            plaintextPassword,
            roleToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement projection = document.RootElement;

        Guid createdIdentifier = projection.GetProperty("operatorIdentifier").GetGuid();
        Assert.Equal("active", projection.GetProperty("state").GetString());
        Assert.Equal(roleToken, projection.GetProperty("role").GetString());

        string[] approvedFields = ["operatorIdentifier", "state", "role"];
        Assert.Equal(approvedFields.Length, projection.EnumerateObject().Count());
        foreach (JsonProperty property in projection.EnumerateObject())
        {
            Assert.Contains(property.Name, approvedFields);
        }

        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/operators/{createdIdentifier:D}", response.Headers.Location!.ToString());

        Operator persisted = await ReadOperatorAsync(createdIdentifier);
        Assert.Equal(loginIdentifier, persisted.UserName);
        Assert.Equal(loginIdentifier.ToUpperInvariant(), persisted.NormalizedUserName);
        Assert.Equal(OperatorState.Active, persisted.State);
        Assert.Equal(roleToken, RoleToken(persisted.Role));
        Assert.Equal(Operator.InitialAuthorizationStateVersion, persisted.AuthorizationStateVersion);
        Assert.NotEqual(plaintextPassword, persisted.PasswordHash);
        Assert.DoesNotContain(plaintextPassword, persisted.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, IdentityPassword.Verify(persisted, plaintextPassword));
    }

    [Fact]
    public async Task SuccessWritesExactlyOneProductAuditInSameTransactionAsOperatorRow()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.audit.admin", OperatorRole.Administrator);
        const string correlationId = "opr-create-success-audit";
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            correlationId,
            "opr.create.audit.target",
            "Second-Operator-Password-789!",
            "teller");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid createdIdentifier = document.RootElement.GetProperty("operatorIdentifier").GetGuid();

        Assert.Equal(operatorCountBefore + 1, await CountOperatorsAsync());

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal(OperatorPersistence.AdministratorRoleToken, audit.ActorRole);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal(createdIdentifier.ToString("D"), audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.SuccessResultToken, audit.Result);
        Assert.Null(audit.FailureBusinessErrorCode);
    }

    [Theory]
    [InlineData(null, "Valid-Password-1!", "administrator")]
    [InlineData("", "Valid-Password-1!", "administrator")]
    [InlineData("   ", "Valid-Password-1!", "administrator")]
    [InlineData("opr.create.invalid.cred", null, "administrator")]
    [InlineData("opr.create.invalid.cred", "", "administrator")]
    [InlineData("opr.create.invalid.cred", "   ", "administrator")]
    public async Task InvalidCredentialReturns400WithHandlerRejectionAuditExactlyOnce(
        string? loginIdentifier,
        string? password,
        string role)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            $"opr.create.invalid.cred.admin.{Guid.NewGuid():N}",
            OperatorRole.Administrator);
        string correlationId = $"opr-create-invalid-cred-{Guid.NewGuid():N}";
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            correlationId,
            loginIdentifier,
            password,
            role);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(administrator.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task MissingLoginIdentifierPropertyReturns400WithHandlerRejectionAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.absent.field.admin", OperatorRole.Administrator);
        const string correlationId = "opr-create-absent-field";
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(administrator));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = new StringContent(
            """{"password":"Valid-Password-1!","role":"administrator"}""",
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
        Assert.Single(await ReadAuditsAsync(correlationId));
    }

    [Fact]
    public async Task HandlerOwnedValidationInputsReachActionAndWriteExactlyOneRejectionAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            "opr.create.validation-boundary.admin",
            OperatorRole.Administrator);
        long operatorCountBefore = await CountOperatorsAsync();
        OperatorCreateActionReachProbe reachProbe = new();

        HandlerValidationCase[] cases =
        [
            new("body-missing", null),
            new("json-null", "null"),
            new("missing-loginIdentifier", "{\"password\":\"Valid-Password-1!\",\"role\":\"administrator\"}"),
            new("missing-password", "{\"loginIdentifier\":\"opr.create.validation.missing-password\",\"role\":\"administrator\"}"),
            new("missing-role", "{\"loginIdentifier\":\"opr.create.validation.missing-role\",\"password\":\"Valid-Password-1!\"}"),
            new("null-loginIdentifier", "{\"loginIdentifier\":null,\"password\":\"Valid-Password-1!\",\"role\":\"administrator\"}"),
            new("null-password", "{\"loginIdentifier\":\"opr.create.validation.null-password\",\"password\":null,\"role\":\"administrator\"}"),
            new("null-role", "{\"loginIdentifier\":\"opr.create.validation.null-role\",\"password\":\"Valid-Password-1!\",\"role\":null}"),
            new("empty-loginIdentifier", "{\"loginIdentifier\":\"\",\"password\":\"Valid-Password-1!\",\"role\":\"administrator\"}"),
            new("empty-password", "{\"loginIdentifier\":\"opr.create.validation.empty-password\",\"password\":\"\",\"role\":\"administrator\"}"),
            new("empty-role", "{\"loginIdentifier\":\"opr.create.validation.empty-role\",\"password\":\"Valid-Password-1!\",\"role\":\"\"}"),
            new("whitespace-loginIdentifier", "{\"loginIdentifier\":\"   \",\"password\":\"Valid-Password-1!\",\"role\":\"administrator\"}"),
            new("whitespace-password", "{\"loginIdentifier\":\"opr.create.validation.whitespace-password\",\"password\":\"   \",\"role\":\"administrator\"}"),
            new("whitespace-role", "{\"loginIdentifier\":\"opr.create.validation.whitespace-role\",\"password\":\"Valid-Password-1!\",\"role\":\"   \"}"),
            new("invalid-role", "{\"loginIdentifier\":\"opr.create.validation.invalid-role\",\"password\":\"Valid-Password-1!\",\"role\":\"owner\"}"),
            new("wrong-case-role", "{\"loginIdentifier\":\"opr.create.validation.wrong-case-role\",\"password\":\"Valid-Password-1!\",\"role\":\"Administrator\"}"),
        ];

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.Configure<MvcOptions>(options =>
                options.Filters.Add(new OperatorCreateActionReachProbeFilter(reachProbe)));
        });
        using HttpClient client = factory.CreateClient();

        for (int index = 0; index < cases.Length; index++)
        {
            HandlerValidationCase validationCase = cases[index];
            string correlationId = $"opr-create-validation-boundary-{validationCase.Name}";

            using HttpResponseMessage response = await SendRawCreateAsync(
                client,
                CreateToken(administrator),
                correlationId,
                validationCase.Body);

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(index + 1, reachProbe.InvocationCount);
            Assert.Equal(operatorCountBefore, await CountOperatorsAsync());

            PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
            Assert.Equal("operator.command.create", audit.OperationIdentifier);
            Assert.Equal("operators", audit.TargetIdentifier);
            Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
            Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
        }
    }

    [Fact]
    public async Task FrameworkOwnedRejectionsDoNotReachActionOrWriteProductAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            "opr.create.framework-boundary.admin",
            OperatorRole.Administrator);
        long operatorCountBefore = await CountOperatorsAsync();
        OperatorCreateActionReachProbe reachProbe = new();

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.Configure<MvcOptions>(options =>
                options.Filters.Add(new OperatorCreateActionReachProbeFilter(reachProbe)));
        });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage malformedJson = await SendRawCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-framework-malformed-json",
            "{\"loginIdentifier\":");
        await AssertErrorAsync(malformedJson, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(0, reachProbe.InvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-create-framework-malformed-json"));

        using HttpResponseMessage unsupportedContentType = await SendRawCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-framework-unsupported-content-type",
            "not-json",
            "text/plain");
        await AssertStatusOnlyAsync(unsupportedContentType, HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, reachProbe.InvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-create-framework-unsupported-content-type"));
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Administrator")]
    [InlineData("ADMINISTRATOR")]
    [InlineData("owner")]
    public async Task InvalidRoleReturns400WithHandlerRejectionAuditExactlyOnce(string? role)
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            $"opr.create.invalid.role.admin.{Guid.NewGuid():N}",
            OperatorRole.Administrator);
        string correlationId = $"opr-create-invalid-role-{Guid.NewGuid():N}";
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            correlationId,
            $"opr.create.invalid.role.{Guid.NewGuid():N}",
            "Valid-Password-1!",
            role);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("validation_failed", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task DuplicateNormalizedLoginIdentifierReturns409WithoutPartialRowAndAuditExactlyOnce()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.dup.admin", OperatorRole.Administrator);
        Operator existing = await SeedOperatorAsync("opr.create.dup.target", OperatorRole.Viewer);
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage exactMatch = await SendCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-dup-exact",
            existing.UserName,
            "Another-Password-1!",
            "administrator");
        await AssertErrorAsync(exactMatch, HttpStatusCode.Conflict, "operator_login_identifier_already_registered");

        using HttpResponseMessage caseDifferent = await SendCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-dup-case",
            existing.UserName.ToUpperInvariant(),
            "Another-Password-2!",
            "administrator");
        await AssertErrorAsync(caseDifferent, HttpStatusCode.Conflict, "operator_login_identifier_already_registered");

        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());

        foreach (string correlationId in new[] { "opr-create-dup-exact", "opr-create-dup-case" })
        {
            PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
            Assert.Equal(administrator.Id, audit.ActorIdentifier);
            Assert.Equal("operator.command.create", audit.OperationIdentifier);
            Assert.Equal("operators", audit.TargetIdentifier);
            Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
            Assert.Equal("operator_login_identifier_already_registered", audit.FailureBusinessErrorCode);
        }
    }

    [Fact]
    public async Task UnauthenticatedRequestReturns401WithoutReachingHandlerOrWritingProductAudit()
    {
        await MigrateAsync();
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators")
        {
            Content = JsonContent.Create(new
            {
                loginIdentifier = "opr.create.unauthenticated",
                password = "Valid-Password-1!",
                role = "administrator",
            }),
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Theory]
    [InlineData(OperatorRole.Viewer)]
    [InlineData(OperatorRole.Teller)]
    public async Task NonAdministratorReturns403WithoutHandlerReachAndWritesOneAuthzAudit(OperatorRole nonAdminRole)
    {
        await MigrateAsync();
        Operator nonAdmin = await SeedOperatorAsync(
            $"opr.create.non-admin.{nonAdminRole}".ToLowerInvariant(),
            nonAdminRole);
        string correlationId = $"opr-create-non-admin-{nonAdminRole}".ToLowerInvariant();
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(nonAdmin),
            correlationId,
            "opr.create.blocked-by-403",
            "Valid-Password-1!",
            "administrator");

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "operation_not_permitted");
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());

        PersistedAudit audit = Assert.Single(await ReadAuditsAsync(correlationId));
        Assert.Equal(nonAdmin.Id, audit.ActorIdentifier);
        Assert.Equal("operator.command.create", audit.OperationIdentifier);
        Assert.Equal("operators", audit.TargetIdentifier);
        Assert.Equal(AuditPersistence.FailureResultToken, audit.Result);
        Assert.Equal("operation_not_permitted", audit.FailureBusinessErrorCode);
    }

    [Fact]
    public async Task PolicyAndHandlerRejectionsDoNotDoubleAudit()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("opr.create.no-double.viewer", OperatorRole.Viewer);
        Operator administrator = await SeedOperatorAsync("opr.create.no-double.admin", OperatorRole.Administrator);

        await using OperatorCreateApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage forbidden = await SendCreateAsync(
            client,
            CreateToken(viewer),
            "opr-create-no-double-policy",
            "opr.create.no-double.policy-target",
            "Valid-Password-1!",
            "administrator");
        using HttpResponseMessage invalidRole = await SendCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-no-double-handler",
            "opr.create.no-double.handler-target",
            "Valid-Password-1!",
            "owner");

        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, "operation_not_permitted");
        await AssertErrorAsync(invalidRole, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Single(await ReadAuditsAsync("opr-create-no-double-policy"));
        Assert.Single(await ReadAuditsAsync("opr-create-no-double-handler"));
    }

    [Fact]
    public async Task RequiredSuccessAuditFailureLeavesNoOperatorRowAndNoSuccessResponse()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.audit-failure.admin", OperatorRole.Administrator);
        AuditFailureProbe failureProbe = new();
        const string normalizedTarget = "OPR.CREATE.AUDIT-FAILURE.TARGET";
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(failureProbe);
            services.AddScoped<IAuditWriter, FailingSuccessAuditWriter>();
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            "opr-create-audit-failure",
            "opr.create.audit-failure.target",
            "Valid-Password-1!",
            "administrator");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("operatorIdentifier", body, StringComparison.Ordinal);
        Assert.Equal(1, failureProbe.InvocationCount);
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
        Assert.False(await OperatorExistsAsync(normalizedTarget));
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task HandlerRejectionAuditFailureFailsClosedWithoutResponseOrAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            "opr.create.rejection-audit-failure.admin",
            OperatorRole.Administrator);
        const string correlationId = "opr-create-rejection-audit-failure";
        const string normalizedTarget = "OPR.CREATE.REJECTION-AUDIT-FAILURE.TARGET";
        AuditPersistenceFailureProbe persistenceFailureProbe = new();
        ResultFactoryProbe resultFactoryProbe = new();
        ThrowOnOperatorCreateAuditSaveChangesInterceptor persistenceFailure =
            new(persistenceFailureProbe);
        long operatorCountBefore = await CountOperatorsAsync();

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(persistenceFailure);
            services.AddSingleton(resultFactoryProbe);
            services.AddDbContext<BankDbContext>((serviceProvider, options) =>
                options.AddInterceptors(
                    serviceProvider.GetRequiredService<ThrowOnOperatorCreateAuditSaveChangesInterceptor>()));
            services.AddScoped<IAuditWriter>(serviceProvider =>
                new ResultFactoryProbeAuditWriter(
                    ActivatorUtilities.CreateInstance<PostgreSqlAuditWriter>(serviceProvider),
                    serviceProvider.GetRequiredService<ResultFactoryProbe>()));
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await SendCreateAsync(
            client,
            CreateToken(administrator),
            correlationId,
            "opr.create.rejection-audit-failure.target",
            "Valid-Password-1!",
            "owner");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_failed", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "operator_login_identifier_already_registered",
            body,
            StringComparison.Ordinal);
        Assert.Equal(1, persistenceFailureProbe.FailurePointHitCount);
        Assert.Equal(1, persistenceFailureProbe.AddedAuditRecordCount);
        Assert.Equal(1, persistenceFailureProbe.FailureInjectionCount);
        Assert.Equal(0, resultFactoryProbe.InvocationCount);
        Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
        Assert.False(await OperatorExistsAsync(normalizedTarget));
        Assert.Empty(await ReadAuditsAsync(correlationId));
        Assert.Equal(0L, await CountAuditsAsync());
    }

    [Fact]
    public async Task OperatorPersistenceFailureBeforeSuccessAuditFailsClosedWithoutAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync(
            "opr.create.persistence-failure.admin",
            OperatorRole.Administrator);
        long operatorCountBefore = await CountOperatorsAsync();
        AuditAppendProbe appendProbe = new();
        await InstallOperatorPersistenceFailureTriggerAsync();

        try
        {
            await using OperatorCreateApiFactory factory = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(appendProbe);
                services.AddScoped<IAuditWriter>(serviceProvider =>
                    new RecordingAuditWriter(
                        ActivatorUtilities.CreateInstance<PostgreSqlAuditWriter>(serviceProvider),
                        serviceProvider.GetRequiredService<AuditAppendProbe>()));
            });
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await SendCreateAsync(
                client,
                CreateToken(administrator),
                "opr-create-operator-persistence-failure",
                "opr.create.operator-persistence-failure.target",
                "Valid-Password-1!",
                "administrator");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains("internal_error", body, StringComparison.Ordinal);
            Assert.Equal(0, appendProbe.CurrentTransactionInvocationCount);
            Assert.Equal(operatorCountBefore, await CountOperatorsAsync());
            Assert.Equal(0L, await CountAuditsAsync());
        }
        finally
        {
            await RemoveOperatorPersistenceFailureTriggerAsync();
        }

        Assert.False(await OperatorPersistenceFailureTriggerExistsAsync());
    }

    [Fact]
    public async Task ConcurrentDuplicateRequestsProduceExactlyOneSuccessAndOneConflictWithoutMisleadingAudit()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.race.admin", OperatorRole.Administrator);
        const string loginIdentifier = "opr.create.race.target";
        const string normalizedLoginIdentifier = "OPR.CREATE.RACE.TARGET";
        const string firstCorrelationId = "opr-create-race-a";
        const string secondCorrelationId = "opr-create-race-b";
        DuplicateCommitBarrier barrier = new();

        await using OperatorCreateApiFactory factory = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorCreateSuccessCommitter>();
            services.AddSingleton(barrier);
            services.AddScoped<IOperatorCreateSuccessCommitter>(serviceProvider =>
                new CoordinatedOperatorCreateSuccessCommitter(
                    new AtomicOperatorCreateSuccessCommitter(
                        serviceProvider.GetRequiredService<BankDbContext>(),
                        serviceProvider.GetRequiredService<IAuditWriter>()),
                    serviceProvider.GetRequiredService<DuplicateCommitBarrier>()));
        });
        using HttpClient client = factory.CreateClient();
        string token = CreateToken(administrator);

        Task<HttpResponseMessage> first = SendCreateAsync(
            client, token, firstCorrelationId, loginIdentifier, "Race-Password-One-1!", "teller");
        Task<HttpResponseMessage> second = SendCreateAsync(
            client, token, secondCorrelationId, loginIdentifier, "Race-Password-Two-2!", "teller");
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        try
        {
            HttpStatusCode[] statusCodes = [.. responses.Select(response => response.StatusCode).OrderBy(code => code)];
            Assert.Equal(
                new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
                statusCodes);
            Assert.Equal(2, barrier.CommitEntryCount);
            Assert.Equal(1, barrier.UniqueViolationCount);
            Assert.True(barrier.ReleaseCompleted);

            Assert.Equal(1L, await CountOperatorsWithNormalizedNameAsync(normalizedLoginIdentifier));

            IReadOnlyList<PersistedAudit> firstAudits = await ReadAuditsAsync(firstCorrelationId);
            IReadOnlyList<PersistedAudit> secondAudits = await ReadAuditsAsync(secondCorrelationId);
            PersistedAudit[] allAudits = [.. firstAudits, .. secondAudits];

            Assert.Single(allAudits, audit => audit.Result == AuditPersistence.SuccessResultToken);
            PersistedAudit failureAudit = Assert.Single(
                allAudits,
                audit => audit.Result == AuditPersistence.FailureResultToken);
            Assert.Equal("operator_login_identifier_already_registered", failureAudit.FailureBusinessErrorCode);
            Assert.Equal("operators", failureAudit.TargetIdentifier);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task CredentialIsAbsentFromResponseAuditAndTechnicalLogsWithPositiveControl()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("opr.create.disclosure.admin", OperatorRole.Administrator);
        const string credentialSentinel = "OPR-CREATE-CREDENTIAL-SENTINEL-7f3d9c2b41";
        const string correlationId = "opr-create-disclosure";
        string responseBody;
        HttpStatusCode statusCode;
        using ConsoleCapture capture = new();

        await using (OperatorCreateApiFactory factory = CreateFactory())
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await SendCreateAsync(
                   client,
                   CreateToken(administrator),
                   correlationId,
                   "opr.create.disclosure.target",
                   credentialSentinel,
                   "viewer"))
        {
            statusCode = response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync();
        }

        Assert.Equal(HttpStatusCode.Created, statusCode);

        // Positive control: prove the assertion technique used below is capable of failing when
        // the sentinel is actually present in the inspected surface, before trusting it to prove
        // absence.
        Xunit.Sdk.XunitException oracleProof = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            Assert.DoesNotContain(credentialSentinel, responseBody + credentialSentinel, StringComparison.Ordinal));
        Assert.Contains(credentialSentinel, oracleProof.Message, StringComparison.Ordinal);

        Assert.DoesNotContain(credentialSentinel, responseBody, StringComparison.Ordinal);
        foreach (string prohibitedField in new[]
                 {
                     "password",
                     "passwordHash",
                     "securityStamp",
                     "authorizationStateVersion",
                     "jwt",
                     "signingKey",
                     "credential",
                 })
        {
            Assert.DoesNotContain(prohibitedField, responseBody, StringComparison.OrdinalIgnoreCase);
        }

        string auditJson = await ReadAuditRowAsJsonAsync(correlationId);
        Assert.DoesNotContain(credentialSentinel, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("password", auditJson, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(credentialSentinel, capture.Content, StringComparison.Ordinal);
        // Prove the console capture actually observed this request's technical activity, so the
        // absence check above is not a vacuous pass over an empty buffer.
        Assert.Contains(correlationId, capture.Content, StringComparison.Ordinal);
    }

    private OperatorCreateApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Migration failed. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using BankDbContext context = CreateContext();
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.AsNoTracking().SingleAsync(candidate => candidate.Id == identifier);
    }

    private async Task<bool> OperatorExistsAsync(string normalizedUserName)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators
            .AsNoTracking()
            .AnyAsync(candidate => candidate.NormalizedUserName == normalizedUserName);
    }

    private async Task<long> CountOperatorsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"SELECT count(*) FROM {OperatorPersistence.TableName};", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountOperatorsWithNormalizedNameAsync(string normalizedUserName)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*) FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.NormalizedUserNameColumn} = @normalized_user_name;
             """,
            connection);
        command.Parameters.AddWithValue("normalized_user_name", normalizedUserName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountAuditsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"SELECT count(*) FROM {AuditPersistence.TableName};", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task InstallOperatorPersistenceFailureTriggerAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            CREATE FUNCTION public.opr_create_test_reject_operator_insert()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'OPR_CREATE_TEST_OPERATOR_PERSISTENCE_FAILURE';
            END;
            $function$;
            CREATE TRIGGER trg_opr_create_test_reject_operator_insert
            BEFORE INSERT ON operators
            FOR EACH ROW
            EXECUTE FUNCTION public.opr_create_test_reject_operator_insert();
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RemoveOperatorPersistenceFailureTriggerAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            DROP TRIGGER IF EXISTS trg_opr_create_test_reject_operator_insert ON operators;
            DROP FUNCTION IF EXISTS public.opr_create_test_reject_operator_insert();
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> OperatorPersistenceFailureTriggerExistsAsync()
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_trigger
                WHERE tgname = 'trg_opr_create_test_reject_operator_insert');
            """,
            connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<IReadOnlyList<PersistedAudit>> ReadAuditsAsync(string correlationId)
    {
        List<PersistedAudit> audits = [];
        await using NpgsqlConnection connection = new(Database.ConnectionString);
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
            audits.Add(new PersistedAudit(
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

    private async Task<string> ReadAuditRowAsJsonAsync(string correlationId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT row_to_json(a)::text
             FROM {AuditPersistence.TableName} AS a
             WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;
             """,
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<HttpResponseMessage> SendCreateAsync(
        HttpClient client,
        string token,
        string correlationId,
        string? loginIdentifier,
        string? password,
        string? role)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = JsonContent.Create(new
        {
            loginIdentifier,
            password,
            role,
        });
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRawCreateAsync(
        HttpClient client,
        string token,
        string correlationId,
        string? body,
        string mediaType = "application/json")
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertErrorAsync(
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

    private static async Task AssertStatusOnlyAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}. Body: {body}");
    }

    private static string CreateToken(Operator operatorEntity)
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
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string RoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result,
        string? FailureBusinessErrorCode,
        string CorrelationId);

    private sealed record HandlerValidationCase(string Name, string? Body);
}

internal sealed class OperatorCreateApiFactory(
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

internal sealed class AuditFailureProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class AuditPersistenceFailureProbe
{
    private int failurePointHitCount;
    private int addedAuditRecordCount;
    private int failureInjectionCount;

    public int FailurePointHitCount => Volatile.Read(ref failurePointHitCount);

    public int AddedAuditRecordCount => Volatile.Read(ref addedAuditRecordCount);

    public int FailureInjectionCount => Volatile.Read(ref failureInjectionCount);

    public void RecordFailurePointHit(int addedAuditRecords)
    {
        Interlocked.Increment(ref failurePointHitCount);
        Interlocked.Add(ref addedAuditRecordCount, addedAuditRecords);
    }

    public void RecordFailureInjection() => Interlocked.Increment(ref failureInjectionCount);
}

internal sealed class ResultFactoryProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class ThrowOnOperatorCreateAuditSaveChangesInterceptor(
    AuditPersistenceFailureProbe probe) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowForAddedAudit(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ThrowForAddedAudit(eventData);
        return ValueTask.FromResult(result);
    }

    private void ThrowForAddedAudit(DbContextEventData eventData)
    {
        int addedAuditRecords = eventData.Context?.ChangeTracker.Entries<AuditRecord>()
            .Count(entry => entry.State == EntityState.Added) ?? 0;
        if (addedAuditRecords == 0)
        {
            return;
        }

        probe.RecordFailurePointHit(addedAuditRecords);
        probe.RecordFailureInjection();
        throw new OperatorCreateAuditPersistenceFailureInjectionException();
    }
}

internal sealed class ResultFactoryProbeAuditWriter(
    PostgreSqlAuditWriter inner,
    ResultFactoryProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default) =>
        inner.AppendToCurrentTransactionAsync(request, cancellationToken);

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default) =>
        inner.AppendInSeparateTransactionBeforeResultAsync(
            request,
            async resultCancellationToken =>
            {
                probe.RecordInvocation();
                return await successResultFactory(resultCancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
}

internal sealed class OperatorCreateAuditPersistenceFailureInjectionException()
    : InvalidOperationException(
        "Deterministic test-only Operator create AuditRecord persistence failure.");

internal sealed class OperatorCreateActionReachProbe
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);
}

internal sealed class OperatorCreateActionReachProbeFilter(OperatorCreateActionReachProbe probe)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Method == HttpMethods.Post &&
            string.Equals(context.HttpContext.Request.Path, "/operators", StringComparison.Ordinal))
        {
            probe.RecordInvocation();
        }

        await next().ConfigureAwait(false);
    }
}

internal sealed class DuplicateCommitBarrier
{
    private readonly TaskCompletionSource<object?> release = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int commitEntryCount;
    private int uniqueViolationCount;

    public int CommitEntryCount => Volatile.Read(ref commitEntryCount);

    public int UniqueViolationCount => Volatile.Read(ref uniqueViolationCount);

    public bool ReleaseCompleted => release.Task.IsCompletedSuccessfully;

    public async Task WaitForBothCommitEntriesAsync(CancellationToken cancellationToken)
    {
        int entries = Interlocked.Increment(ref commitEntryCount);
        if (entries == 2)
        {
            release.TrySetResult(null);
        }

        await release.Task
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);
    }

    public void RecordUniqueViolation() => Interlocked.Increment(ref uniqueViolationCount);
}

internal sealed class CoordinatedOperatorCreateSuccessCommitter(
    AtomicOperatorCreateSuccessCommitter productionCommitter,
    DuplicateCommitBarrier barrier) : IOperatorCreateSuccessCommitter
{
    public async Task CommitAsync(
        Operator newOperator,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await barrier.WaitForBothCommitEntriesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await productionCommitter
                .CommitAsync(newOperator, actorIdentifier, actorRole, httpContext, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (IsNormalizedUserNameUniqueViolation(failure))
        {
            barrier.RecordUniqueViolation();
            throw;
        }
    }

    private static bool IsNormalizedUserNameUniqueViolation(DbUpdateException failure) =>
        failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresFailure &&
        string.Equals(
            postgresFailure.ConstraintName,
            OperatorPersistence.NormalizedUserNameIndex,
            StringComparison.Ordinal);
}

internal sealed class AuditAppendProbe
{
    private int currentTransactionInvocationCount;

    public int CurrentTransactionInvocationCount =>
        Volatile.Read(ref currentTransactionInvocationCount);

    public void RecordCurrentTransactionInvocation() =>
        Interlocked.Increment(ref currentTransactionInvocationCount);
}

internal sealed class RecordingAuditWriter(
    IAuditWriter inner,
    AuditAppendProbe probe) : IAuditWriter
{
    public async Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        probe.RecordCurrentTransactionInvocation();
        await inner.AppendToCurrentTransactionAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default) =>
        inner.AppendInSeparateTransactionBeforeResultAsync(
            request,
            successResultFactory,
            cancellationToken);
}

/// <summary>
/// Fails on both Audit transaction primitives so it models a required-Audit failure regardless of
/// which primitive the calling code path exercises (the create success path uses
/// <see cref="IAuditWriter.AppendToCurrentTransactionAsync"/>; handler rejections use
/// <see cref="IAuditWriter.AppendInSeparateTransactionBeforeResultAsync{TResult}"/>).
/// </summary>
internal sealed class FailingSuccessAuditWriter(AuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorCreateAuditFailureInjectionException();
    }
}

internal sealed class OperatorCreateAuditFailureInjectionException()
    : InvalidOperationException("Deterministic test-only Operator create Audit persistence failure.");
