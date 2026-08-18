using System.Reflection;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Identity;

public sealed class OperatorTests
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Theory]
    [InlineData(OperatorRole.Administrator)]
    [InlineData(OperatorRole.Teller)]
    [InlineData(OperatorRole.Viewer)]
    public void CreateAssignsApplicationGeneratedUuidV7AndExactlyOneRole(OperatorRole role)
    {
        ApplicationTime time = new(new FrozenTimeProvider(FrozenUtc));
        Operator created = Operator.Create(
            "teller.one",
            new OperatorPasswordHash("identity-hashed-password"),
            role,
            time.GetUtcNow(),
            "security-stamp");

        Assert.Equal(7, created.Id.Version);
        Assert.Equal(role, created.Role);
        Assert.Equal(OperatorState.Active, created.State);
        Assert.Equal(FrozenUtc, created.CreatedAt);
        Assert.Equal(1, created.AuthorizationStateVersion);
        Assert.Null(typeof(Operator).GetProperty("Roles"));
        Assert.Equal(typeof(OperatorRole), typeof(Operator).GetProperty(nameof(Operator.Role))!.PropertyType);
    }

    [Fact]
    public void DefaultRoleIsUnassignedAndRejectedByConstruction()
    {
        Assert.NotEqual(OperatorRole.Administrator, default(OperatorRole));

        Assert.Throws<ArgumentOutOfRangeException>(() => Operator.Create(
            "operator.one",
            new OperatorPasswordHash("identity-hashed-password"),
            default,
            FrozenUtc,
            "security-stamp"));
    }

    [Fact]
    public void OperatorCreateDoesNotAcceptRawPasswordHashStrings()
    {
        MethodInfo create = typeof(Operator)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Operator.Create));

        ParameterInfo passwordHash = create.GetParameters()
            .Single(parameter => parameter.Name == "passwordHash");

        Assert.Equal(typeof(OperatorPasswordHash), passwordHash.ParameterType);
        Assert.NotEqual(typeof(string), passwordHash.ParameterType);
        Assert.Empty(typeof(OperatorPasswordHash).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void CreateRejectsBlankUserName()
    {
        Assert.Throws<ArgumentException>(() => Operator.Create(
            "  ",
            new OperatorPasswordHash("hash"),
            OperatorRole.Viewer,
            FrozenUtc,
            "stamp"));
    }

    [Fact]
    public void EnableBumpsAuthorizationStateSecurityStampAndUpdatedAt()
    {
        Operator created = CreateOperator(OperatorRole.Teller);
        created.Disable(FrozenUtc, "disable-stamp");
        int versionAfterDisable = created.AuthorizationStateVersion;
        DateTimeOffset laterUtc = FrozenUtc.AddMinutes(5);

        created.Enable(laterUtc, "enable-stamp");

        Assert.Equal(OperatorState.Active, created.State);
        Assert.Equal(versionAfterDisable + 1, created.AuthorizationStateVersion);
        Assert.Equal("enable-stamp", created.SecurityStamp);
        Assert.Equal(laterUtc, created.UpdatedAt);
    }

    [Fact]
    public void DisableBumpsAuthorizationStateSecurityStampAndUpdatedAt()
    {
        Operator created = CreateOperator(OperatorRole.Teller);
        int initialVersion = created.AuthorizationStateVersion;
        DateTimeOffset laterUtc = FrozenUtc.AddMinutes(5);

        created.Disable(laterUtc, "disable-stamp");

        Assert.Equal(OperatorState.Disabled, created.State);
        Assert.Equal(initialVersion + 1, created.AuthorizationStateVersion);
        Assert.Equal("disable-stamp", created.SecurityStamp);
        Assert.Equal(laterUtc, created.UpdatedAt);
    }

    [Fact]
    public void ChangeRoleBumpsAuthorizationStateSecurityStampAndUpdatedAt()
    {
        Operator created = CreateOperator(OperatorRole.Teller);
        int initialVersion = created.AuthorizationStateVersion;
        DateTimeOffset laterUtc = FrozenUtc.AddMinutes(5);

        created.ChangeRole(OperatorRole.Administrator, laterUtc, "role-stamp");

        Assert.Equal(OperatorRole.Administrator, created.Role);
        Assert.Equal(initialVersion + 1, created.AuthorizationStateVersion);
        Assert.Equal("role-stamp", created.SecurityStamp);
        Assert.Equal(laterUtc, created.UpdatedAt);
    }

    [Fact]
    public void ChangeRoleRejectsUnspecifiedRole()
    {
        Operator created = CreateOperator(OperatorRole.Teller);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            created.ChangeRole(OperatorRole.Unspecified, FrozenUtc, "stamp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MutationMethodsRejectBlankSecurityStamp(string? blankStamp)
    {
        Operator created = CreateOperator(OperatorRole.Teller);

        Assert.ThrowsAny<ArgumentException>(() => created.Enable(FrozenUtc, blankStamp!));
        Assert.ThrowsAny<ArgumentException>(() => created.Disable(FrozenUtc, blankStamp!));
        Assert.ThrowsAny<ArgumentException>(() =>
            created.ChangeRole(OperatorRole.Administrator, FrozenUtc, blankStamp!));
    }

    private static Operator CreateOperator(OperatorRole role) =>
        Operator.Create(
            "mutation.subject",
            new OperatorPasswordHash("identity-hashed-password"),
            role,
            FrozenUtc,
            "initial-stamp");
}

file sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
