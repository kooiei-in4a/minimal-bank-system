namespace MinimalBankSystem.Domain.Identity;

/// <summary>
/// Pure last-active-administrator invariant evaluated after the active-administrator set is locked.
/// </summary>
public static class ActiveAdministratorSet
{
    public static bool MutationWouldLeaveZeroActiveAdministrators(
        Operator target,
        bool removesFromActiveAdministratorSet,
        IReadOnlyList<Guid> lockedActiveAdministratorIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(lockedActiveAdministratorIdentifiers);

        if (!removesFromActiveAdministratorSet ||
            target.State != OperatorState.Active ||
            target.Role != OperatorRole.Administrator)
        {
            return false;
        }

        return !lockedActiveAdministratorIdentifiers.Contains(target.Id)
            || lockedActiveAdministratorIdentifiers.Count <= 1;
    }
}
