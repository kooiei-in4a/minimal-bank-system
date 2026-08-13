namespace MinimalBankSystem.Domain.Identity;

/// <summary>
/// The three fixed product roles. Stored as a single lowercase text value so an Operator
/// cannot have zero or multiple current roles.
/// </summary>
public enum OperatorRole
{
    Administrator = 0,
    Teller = 1,
    Viewer = 2,
}
