using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <summary>Adds Product Audit storage and its PostgreSQL append-only trigger.</summary>
public partial class AddProductAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_records",
            columns: table => new
            {
                audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_identifier = table.Column<Guid>(type: "uuid", nullable: false),
                actor_role = table.Column<string>(type: "text", nullable: false),
                operation_identifier = table.Column<string>(type: "text", maxLength: 128, nullable: false),
                target_identifier = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                result = table.Column<string>(type: "text", nullable: false),
                failure_business_error_code = table.Column<string>(type: "text", maxLength: 128, nullable: true),
                correlation_id = table.Column<string>(type: "text", maxLength: 64, nullable: false),
                audit_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_records", x => x.audit_id);
                table.CheckConstraint("ck_audit_records_actor_role", "actor_role IN ('administrator', 'teller', 'viewer')");
                table.CheckConstraint("ck_audit_records_failure_code", "(result = 'success' AND failure_business_error_code IS NULL) OR result = 'failure'");
                table.CheckConstraint("ck_audit_records_result", "result IN ('success', 'failure')");
            });

        migrationBuilder.Sql(
            """
            CREATE FUNCTION public.reject_audit_record_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $audit_append_only$
            BEGIN
                RAISE EXCEPTION USING
                    ERRCODE = '55000',
                    MESSAGE = 'Product Audit records are append-only';
            END;
            $audit_append_only$;

            CREATE TRIGGER trg_audit_records_append_only
            BEFORE UPDATE OR DELETE ON audit_records
            FOR EACH ROW
            EXECUTE FUNCTION public.reject_audit_record_mutation();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder;
        throw new NotSupportedException(
            "Downgrading AddProductAudit would destroy immutable Product Audit history. " +
            "ADR-0009 requires verified backup restore into a clean database instead.");
    }
}
