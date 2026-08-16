using System.Reflection;
using System.Text.Json;
using MinimalBankSystem.Api.OperatorCreate;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.IntegrationTests.OperatorCreate;

public sealed class OperatorCreateContractTests
{
    [Theory]
    [InlineData(OperatorPersistence.AdministratorRoleToken, OperatorRole.Administrator)]
    [InlineData(OperatorPersistence.TellerRoleToken, OperatorRole.Teller)]
    [InlineData(OperatorPersistence.ViewerRoleToken, OperatorRole.Viewer)]
    public void RoleParserAcceptsExactLowercaseTokensOnly(string token, OperatorRole expected)
    {
        Assert.True(OperatorCreateContract.TryParseRole(token, out OperatorRole parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("administrator ")]
    [InlineData(" administrator")]
    [InlineData("Administrator")]
    [InlineData("ADMINISTRATOR")]
    [InlineData("admin")]
    [InlineData("teller ")]
    [InlineData("unknown")]
    [InlineData("1")]
    public void RoleParserFailsClosedOnUnexpectedTokens(string? token)
    {
        Assert.False(OperatorCreateContract.TryParseRole(token, out OperatorRole parsed));
        Assert.Equal(OperatorRole.Unspecified, parsed);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("a", true)]
    [InlineData("  login  ", true)]
    public void CredentialContractTreatsMissingEmptyAndWhitespaceAsInvalid(string? value, bool expected)
    {
        Assert.Equal(expected, OperatorCreateContract.HasUsableCredential(value));
    }

    [Fact]
    public void CreateResponseTypeIsClosedAllowlistWithoutCredentialMembers()
    {
        PropertyInfo[] properties = typeof(OperatorCreateResponse).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(3, properties.Length);
        Assert.Contains(properties, property => property.Name == nameof(OperatorCreateResponse.OperatorIdentifier));
        Assert.Contains(properties, property => property.Name == nameof(OperatorCreateResponse.State));
        Assert.Contains(properties, property => property.Name == nameof(OperatorCreateResponse.Role));
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Stamp", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Version", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Login", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuditWriteRequestBuildersNeverCarryCredentialMaterialAndOracleDetectsSentinelLeak()
    {
        const string login = OperatorCreateDisclosureOracle.LoginSentinel;
        const string password = OperatorCreateDisclosureOracle.PasswordSentinel;
        const string hash = OperatorCreateDisclosureOracle.HashSentinel;
        Guid createdId = Guid.Parse("018f4d25-6b1a-7c3d-8e9f-0123456789ab");

        string leaking = OperatorCreateDisclosureOracle.LeakingProjection(password, login, hash);
        Assert.True(
            OperatorCreateDisclosureOracle.Detects(leaking, password, login, hash),
            "The disclosure oracle must fail when credential material is intentionally present.");

        AuditWriteRequest success = OperatorCreateContract.Success(
            Guid.Parse("018f4d25-6b1a-7c3d-8e9f-aaaaaaaaaaa1"),
            OperatorRole.Administrator,
            createdId,
            "opr-create-audit-shape");
        AuditWriteRequest rejection = OperatorCreateContract.Rejection(
            Guid.Parse("018f4d25-6b1a-7c3d-8e9f-aaaaaaaaaaa1"),
            OperatorRole.Administrator,
            ApiErrorEnvelope.ValidationFailed.Code,
            "opr-create-audit-shape");

        string successJson = JsonSerializer.Serialize(success);
        string rejectionJson = JsonSerializer.Serialize(rejection);

        Assert.False(OperatorCreateDisclosureOracle.Detects(successJson, password, login, hash));
        Assert.False(OperatorCreateDisclosureOracle.Detects(rejectionJson, password, login, hash));
        Assert.Equal(createdId.ToString("D"), success.TargetIdentifier);
        Assert.Equal(OperatorCreateAudit.CollectionTargetIdentifier, rejection.TargetIdentifier);
        Assert.Equal(OperatorCreateAudit.OperationIdentifier, success.OperationIdentifier);
        Assert.Equal(AuditResult.Success, success.Result);
        Assert.Null(success.FailureBusinessErrorCode);
        Assert.Equal(ApiErrorEnvelope.ValidationFailed.Code, rejection.FailureBusinessErrorCode);

        PropertyInfo[] auditProperties = typeof(AuditWriteRequest).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(
            auditProperties,
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Login", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateErrorEnvelopeUsesApprovedExternalCode()
    {
        Assert.Equal(
            "operator_login_identifier_already_registered",
            ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered.Code);
        Assert.DoesNotContain(
            "password",
            ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnexpectedPersistedTokensFailClosedInsteadOfBeingEmitted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OperatorCreateContract.ToRoleToken((OperatorRole)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OperatorCreateContract.ToStateToken((OperatorState)99));
    }
}
