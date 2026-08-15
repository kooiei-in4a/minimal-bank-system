using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <summary>Product Audit persistence and append-only database control owned by WP2-AUD-01.</summary>
public partial class AddAuditPersistence : Migration
{
    private static readonly string[] CorrelationOperationColumns =
        ["correlation_id", "operation_identifier"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_identifier = table.Column<Guid>(type: "uuid", nullable: false),
                actor_role = table.Column<string>(type: "text", nullable: false),
                operation_identifier = table.Column<string>(type: "text", maxLength: 100, nullable: false),
                target_identifier = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                result = table.Column<string>(type: "text", nullable: false),
                failure_business_error_code = table.Column<string>(type: "text", maxLength: 100, nullable: true),
                correlation_id = table.Column<string>(type: "text", maxLength: 64, nullable: false),
                audit_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_logs", x => x.audit_id);
                table.CheckConstraint("ck_audit_logs_actor_role", "actor_role IN ('administrator', 'teller', 'viewer')");
                table.CheckConstraint("ck_audit_logs_failure_code", "(result = 'success' AND failure_business_error_code IS NULL) OR (result = 'failure' AND failure_business_error_code IS NOT NULL)");
                table.CheckConstraint("ck_audit_logs_result", "result IN ('success', 'failure')");
            });

        migrationBuilder.CreateIndex(
            name: "ux_audit_logs_correlation_operation",
            table: "audit_logs",
            columns: CorrelationOperationColumns,
            unique: true);

        migrationBuilder.Sql(
            """
            CREATE FUNCTION public.reject_audit_log_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $audit_append_only$
            BEGIN
                RAISE EXCEPTION USING
                    ERRCODE = '55000',
                    MESSAGE = 'Product Audit history is append-only.';
            END;
            $audit_append_only$;

            CREATE TRIGGER trg_audit_logs_append_only
            BEFORE UPDATE OR DELETE ON public.audit_logs
            FOR EACH ROW
            EXECUTE FUNCTION public.reject_audit_log_mutation();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A schema-only downgrade would destroy immutable Product Audit evidence. ADR-0009
        // therefore requires a verified backup restore into a clean database instead of a
        // superficial DROP TABLE Down path.
        migrationBuilder.Sql(
            """
            DO $audit_rollback$
            BEGIN
                RAISE EXCEPTION USING
                    ERRCODE = '55000',
                    MESSAGE = 'AUDIT_ROLLBACK_REQUIRES_BACKUP_RESTORE';
            END;
            $audit_rollback$;
            """);
    }
}
