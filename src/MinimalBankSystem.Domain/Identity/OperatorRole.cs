namespace MinimalBankSystem.Domain.Identity;

/// <summary>
/// The fixed product roles. The zero sentinel is deliberately not a product role and is
/// rejected before persistence. Valid roles are stored as one lowercase text value so an
/// Operator cannot have zero or multiple current roles.
/// </summary>
public enum OperatorRole
{
    Unspecified = 0,
    Administrator = 1,
    Teller = 2,
    Viewer = 3,
}
