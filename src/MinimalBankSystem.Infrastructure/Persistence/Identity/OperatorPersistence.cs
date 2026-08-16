using Npgsql;

namespace MinimalBankSystem.Infrastructure.Persistence.Identity;

/// <summary>Physical names for the Operator identity schema owned by WP2-ID-01.</summary>
public static class OperatorPersistence
{
    public const string TableName = "operators";
    public const string IdColumn = "id";
    public const string UserNameColumn = "user_name";
    public const string NormalizedUserNameColumn = "normalized_user_name";
    public const string PasswordHashColumn = "password_hash";
    public const string SecurityStampColumn = "security_stamp";
    public const string StateColumn = "state";
    public const string FixedRoleColumn = "fixed_role";
    public const string AuthorizationStateVersionColumn = "authorization_state_version";
    public const string CreatedAtColumn = "created_at";
    public const string UpdatedAtColumn = "updated_at";
    public const string StateCheckConstraint = "ck_operators_state";
    public const string RoleCheckConstraint = "ck_operators_fixed_role";
    public const string NormalizedUserNameIndex = "ix_operators_normalized_user_name";

    public const string ActiveStateToken = "active";
    public const string DisabledStateToken = "disabled";
    public const string AdministratorRoleToken = "administrator";
    public const string TellerRoleToken = "teller";
    public const string ViewerRoleToken = "viewer";

    public const string IdentityMigrationId = "20260813181449_AddOperatorIdentity";

    public static bool IsNormalizedUserNameConflict(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(
                    postgres.ConstraintName,
                    NormalizedUserNameIndex,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
