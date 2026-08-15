namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

/// <summary>Physical names for the Product Audit schema owned by WP2-AUD-01.</summary>
public static class AuditPersistence
{
    public const string TableName = "audit_logs";
    public const string AuditIdColumn = "audit_id";
    public const string ActorIdentifierColumn = "actor_identifier";
    public const string ActorRoleColumn = "actor_role";
    public const string OperationIdentifierColumn = "operation_identifier";
    public const string TargetIdentifierColumn = "target_identifier";
    public const string ResultColumn = "result";
    public const string FailureBusinessErrorCodeColumn = "failure_business_error_code";
    public const string CorrelationIdColumn = "correlation_id";
    public const string AuditTimeColumn = "audit_time";

    public const string ActorRoleCheckConstraint = "ck_audit_logs_actor_role";
    public const string ResultCheckConstraint = "ck_audit_logs_result";
    public const string FailureCodeCheckConstraint = "ck_audit_logs_failure_code";
    public const string OperationCorrelationUniqueIndex = "ux_audit_logs_correlation_operation";
    public const string AppendOnlyFunction = "reject_audit_log_mutation";
    public const string AppendOnlyTrigger = "trg_audit_logs_append_only";

    public const string AdministratorRoleToken = "administrator";
    public const string TellerRoleToken = "teller";
    public const string ViewerRoleToken = "viewer";
    public const string SuccessResultToken = "success";
    public const string FailureResultToken = "failure";

    public const string AuditMigrationId = "20260814234810_AddAuditPersistence";
    public const string RollbackRequiresBackupRestoreSignature =
        "AUDIT_ROLLBACK_REQUIRES_BACKUP_RESTORE";
}
