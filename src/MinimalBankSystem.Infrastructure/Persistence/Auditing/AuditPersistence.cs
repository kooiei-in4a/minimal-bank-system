namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

/// <summary>Physical Product Audit schema and append-only control names.</summary>
public static class AuditPersistence
{
    public const string TableName = "audit_records";
    public const string IdColumn = "audit_id";
    public const string ActorIdentifierColumn = "actor_identifier";
    public const string ActorRoleColumn = "actor_role";
    public const string OperationIdentifierColumn = "operation_identifier";
    public const string TargetIdentifierColumn = "target_identifier";
    public const string ResultColumn = "result";
    public const string FailureBusinessErrorCodeColumn = "failure_business_error_code";
    public const string CorrelationIdColumn = "correlation_id";
    public const string AuditTimeColumn = "audit_time";

    public const string ActorRoleCheckConstraint = "ck_audit_records_actor_role";
    public const string ResultCheckConstraint = "ck_audit_records_result";
    public const string FailureCodeCheckConstraint = "ck_audit_records_failure_code";
    public const string AppendOnlyFunction = "reject_audit_record_mutation";
    public const string AppendOnlyTrigger = "trg_audit_records_append_only";
    public const string AuditMigrationId = "20260814234713_AddProductAudit";

    public const string AdministratorRoleToken = "administrator";
    public const string TellerRoleToken = "teller";
    public const string ViewerRoleToken = "viewer";
    public const string SuccessResultToken = "success";
    public const string FailureResultToken = "failure";
}
