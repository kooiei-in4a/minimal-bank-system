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
    public void ApplyLifecycleMutationUpdatesSecurityMaterialVersionRoleStateAndUpdatedAt()
    {
        Operator operatorEntity = Operator.Create(
            "operator.lifecycle",
            new OperatorPasswordHash("hash"),
            OperatorRole.Viewer,
            FrozenUtc,
            "initial-stamp");
        DateTimeOffset updatedAt = FrozenUtc.AddMinutes(1);

        operatorEntity.ApplyLifecycleMutation(
            OperatorState.Disabled,
            OperatorRole.Teller,
            updatedAt,
            "updated-stamp");

        Assert.Equal(OperatorState.Disabled, operatorEntity.State);
        Assert.Equal(OperatorRole.Teller, operatorEntity.Role);
        Assert.Equal("updated-stamp", operatorEntity.SecurityStamp);
        Assert.Equal(2, operatorEntity.AuthorizationStateVersion);
        Assert.Equal(updatedAt, operatorEntity.UpdatedAt);
    }
}

file sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
