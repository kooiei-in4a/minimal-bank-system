using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Identity;

public sealed class OperatorTests
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void CreateAssignsApplicationGeneratedUuidV7AndExactlyOneRole()
    {
        ApplicationTime time = new(new FrozenTimeProvider(FrozenUtc));
        Operator created = Operator.Create(
            "teller.one",
            "identity-hashed-password",
            OperatorRole.Teller,
            time.GetUtcNow(),
            "security-stamp");

        Assert.Equal(7, created.Id.Version);
        Assert.Equal(OperatorRole.Teller, created.Role);
        Assert.Equal(OperatorState.Active, created.State);
        Assert.Equal(FrozenUtc, created.CreatedAt);
        Assert.Equal(1, created.AuthorizationStateVersion);
        Assert.Null(typeof(Operator).GetProperty("Roles"));
        Assert.Equal(typeof(OperatorRole), typeof(Operator).GetProperty(nameof(Operator.Role))!.PropertyType);
    }

    [Fact]
    public void CreateRejectsBlankUserName()
    {
        Assert.Throws<ArgumentException>(() => Operator.Create(
            "  ",
            "hash",
            OperatorRole.Viewer,
            FrozenUtc,
            "stamp"));
    }
}

file sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
