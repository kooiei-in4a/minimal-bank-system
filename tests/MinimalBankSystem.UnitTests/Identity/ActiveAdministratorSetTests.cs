using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Identity;

public sealed class ActiveAdministratorSetTests
{
    private static readonly DateTimeOffset FrozenUtc = new(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void DisableOfLastLockedActiveAdministratorIsAViolation()
    {
        Operator lastAdmin = Create(OperatorRole.Administrator, "last.admin");
        Assert.True(ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            lastAdmin,
            removesFromActiveAdministratorSet: true,
            [lastAdmin.Id]));
    }

    [Fact]
    public void DisableOfOneOfTwoLockedActiveAdministratorsIsNotAViolation()
    {
        Operator first = Create(OperatorRole.Administrator, "admin.one");
        Operator second = Create(OperatorRole.Administrator, "admin.two");
        Assert.False(ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            first,
            removesFromActiveAdministratorSet: true,
            [first.Id, second.Id]));
    }

    [Fact]
    public void DemotionOfNonAdministratorIsNotAViolation()
    {
        Operator teller = Create(OperatorRole.Teller, "teller.one");
        Operator admin = Create(OperatorRole.Administrator, "admin.one");
        Assert.False(ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            teller,
            removesFromActiveAdministratorSet: true,
            [admin.Id]));
    }

    [Fact]
    public void PromotionDoesNotRemoveFromTheActiveAdministratorSet()
    {
        Operator teller = Create(OperatorRole.Teller, "teller.promote");
        Assert.False(ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            teller,
            removesFromActiveAdministratorSet: false,
            []));
    }

    [Fact]
    public void ActiveAdministratorMissingFromLockedSetFailsClosed()
    {
        Operator admin = Create(OperatorRole.Administrator, "admin.missing-from-lock");
        Operator otherA = Create(OperatorRole.Administrator, "admin.other.a");
        Operator otherB = Create(OperatorRole.Administrator, "admin.other.b");
        Assert.True(ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            admin,
            removesFromActiveAdministratorSet: true,
            [otherA.Id, otherB.Id]));
    }

    private static Operator Create(OperatorRole role, string userName) =>
        Operator.Create(
            userName,
            new OperatorPasswordHash("identity-hashed-password"),
            role,
            FrozenUtc,
            "security-stamp");
}
