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
    public void DisableThenEnableBumpsAuthorizationStateAndUpdatesStampAndUpdatedAt()
    {
        Operator created = CreateViewer("opr.mut.enable");
        string originalStamp = created.SecurityStamp;
        DateTimeOffset disabledAt = FrozenUtc.AddMinutes(1);
        DateTimeOffset enabledAt = FrozenUtc.AddMinutes(2);

        created.Disable(disabledAt, "stamp-disabled");
        Assert.Equal(OperatorState.Disabled, created.State);
        Assert.Equal(2, created.AuthorizationStateVersion);
        Assert.Equal("stamp-disabled", created.SecurityStamp);
        Assert.Equal(disabledAt, created.UpdatedAt);

        created.Enable(enabledAt, "stamp-enabled");
        Assert.Equal(OperatorState.Active, created.State);
        Assert.Equal(OperatorRole.Viewer, created.Role);
        Assert.Equal(3, created.AuthorizationStateVersion);
        Assert.Equal("stamp-enabled", created.SecurityStamp);
        Assert.Equal(enabledAt, created.UpdatedAt);
        Assert.NotEqual(originalStamp, created.SecurityStamp);
    }

    [Fact]
    public void ChangeRoleBumpsAuthorizationStateAndRejectsNoOpAndUnknownRoles()
    {
        Operator created = CreateViewer("opr.mut.role");

        created.ChangeRole(OperatorRole.Teller, FrozenUtc.AddMinutes(3), "stamp-role");
        Assert.Equal(OperatorRole.Teller, created.Role);
        Assert.Equal(OperatorState.Active, created.State);
        Assert.Equal(2, created.AuthorizationStateVersion);
        Assert.Equal("stamp-role", created.SecurityStamp);

        Assert.Throws<InvalidOperationException>(
            () => created.ChangeRole(OperatorRole.Teller, FrozenUtc, "stamp-same"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => created.ChangeRole(OperatorRole.Unspecified, FrozenUtc, "stamp-invalid"));
        Assert.Throws<InvalidOperationException>(() => created.Enable(FrozenUtc, "stamp-already-active"));
        created.Disable(FrozenUtc.AddMinutes(4), "stamp-disabled-again");
        Assert.Throws<InvalidOperationException>(() => created.Disable(FrozenUtc, "stamp-already-disabled"));
    }

    private static Operator CreateViewer(string userName) =>
        Operator.Create(
            userName,
            new OperatorPasswordHash("identity-hashed-password"),
            OperatorRole.Viewer,
            FrozenUtc,
            "security-stamp");
}

file sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
