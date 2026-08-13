using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using MinimalBankSystem.Infrastructure.Identity;

namespace MinimalBankSystem.IntegrationTests.Identity;

/// <summary>C# type-level checks for <see cref="Operator"/>, <see cref="OperatorFactory"/> and <see cref="OperatorPasswordHasher"/>; no database required.</summary>
public sealed class OperatorModelTests
{
    private static readonly TimeProvider FixedTimeProvider =
        new FakeTimeProvider(DateTimeOffset.Parse("2026-08-13T18:00:00Z", CultureInfo.InvariantCulture));

    [Fact]
    public void CreatedOperatorHasAVersion7Id()
    {
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, "model-test-user", "P@ssw0rd!12345", OperatorRole.Viewer);

        // RFC 9562 places the version nibble as the first hex digit of the third group
        // ("xxxxxxxx-xxxx-Vxxx-..."), independent of Guid.ToByteArray()'s field-endianness.
        Assert.Equal('7', created.Id.ToString().Split('-')[2][0]);
        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Fact]
    public void CreatedOperatorIdIsDerivedFromTheInjectedTimeProviderNotTheSystemClock()
    {
        Operator early = OperatorFactory.Create(
            new FakeTimeProvider(DateTimeOffset.Parse("2020-01-01T00:00:00Z", CultureInfo.InvariantCulture)),
            "model-test-early",
            "P@ssw0rd!12345",
            OperatorRole.Viewer);
        Operator late = OperatorFactory.Create(
            new FakeTimeProvider(DateTimeOffset.Parse("2030-01-01T00:00:00Z", CultureInfo.InvariantCulture)),
            "model-test-late",
            "P@ssw0rd!12345",
            OperatorRole.Viewer);

        // UUIDv7 is time-ordered: an id minted for an earlier instant sorts before one minted for
        // a later instant, which would not hold if the id ignored the injected TimeProvider.
        Assert.True(early.Id.CompareTo(late.Id) < 0);
    }

    [Fact]
    public void CreatedOperatorNormalizesTheUserNameToUpperInvariant()
    {
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, "Mixed-Case.User", "P@ssw0rd!12345", OperatorRole.Teller);

        Assert.Equal("Mixed-Case.User", created.UserName);
        Assert.Equal("MIXED-CASE.USER", created.NormalizedUserName);
    }

    [Fact]
    public void CreatedOperatorTimestampsMatchTheInjectedTimeProviderAndAreEqualOnCreation()
    {
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, "model-test-time", "P@ssw0rd!12345", OperatorRole.Administrator);

        Assert.Equal(FixedTimeProvider.GetUtcNow(), created.CreatedAt);
        Assert.Equal(FixedTimeProvider.GetUtcNow(), created.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, created.CreatedAt.Offset);
    }

    [Fact]
    public void CreatedOperatorDefaultsToAuthorizationStateVersionOne()
    {
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, "model-test-version", "P@ssw0rd!12345", OperatorRole.Administrator);

        Assert.Equal(1, created.AuthorizationStateVersion);
    }

    [Fact]
    public void PasswordHashIsNeverThePlaintextPassword()
    {
        const string plaintext = "Correct-Horse-Battery-Staple-9";
        Operator created = OperatorFactory.Create(FixedTimeProvider, "model-test-hash", plaintext, OperatorRole.Viewer);

        Assert.NotEqual(plaintext, created.PasswordHash);
        Assert.DoesNotContain(plaintext, created.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorPasswordHasherRoundTripsThroughTheRealAspNetCoreIdentityAlgorithm()
    {
        const string plaintext = "Correct-Horse-Battery-Staple-9";
        Operator created = OperatorFactory.Create(FixedTimeProvider, "model-test-verify", plaintext, OperatorRole.Viewer);

        Assert.Equal(
            PasswordVerificationResult.Success,
            OperatorPasswordHasher.VerifyHashedPassword(created, created.PasswordHash, plaintext));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            OperatorPasswordHasher.VerifyHashedPassword(created, created.PasswordHash, "wrong-password"));
    }

    [Fact]
    public void RoleIsASingleScalarPropertyWithNoCollectionOrJoinShape()
    {
        // Structural proof that "multiple current roles" cannot be represented at all: Operator
        // exposes exactly one Role property, and it is not a collection/array/enumerable type (the
        // shape a many-role join table would require on the CLR side).
        PropertyInfo[] roleProperties =
        [
            .. typeof(Operator).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name.Contains("Role", StringComparison.Ordinal)),
        ];

        PropertyInfo roleProperty = Assert.Single(roleProperties);
        Assert.Equal(nameof(Operator.Role), roleProperty.Name);
        Assert.Equal(typeof(OperatorRole), roleProperty.PropertyType);
        Assert.False(typeof(IEnumerable).IsAssignableFrom(roleProperty.PropertyType));
    }

    [Fact]
    public void OperatorRoleUnspecifiedIsExcludedFromTheValidTokenSet()
    {
        string[] validTokens =
        [
            OperatorConfiguration.AdministratorRoleToken,
            OperatorConfiguration.TellerRoleToken,
            OperatorConfiguration.ViewerRoleToken,
        ];

        Assert.DoesNotContain(OperatorConfiguration.UnspecifiedRoleToken, validTokens);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
