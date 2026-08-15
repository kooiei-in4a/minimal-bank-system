using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <summary>Product Audit persistence with the PostgreSQL append-only boundary.</summary>
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
                audit_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                BEFORE UPDATE OR DELETE ON public.audit_records
                FOR EACH ROW
                EXECUTE FUNCTION public.reject_audit_record_mutation();
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Empty history is safely reversible to the immediately previous contract. Existing
        // Product Audit history must instead cross ADR-0009's verified backup/restore boundary.
        migrationBuilder.Sql(
            """
                DO $audit_down_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public.audit_records LIMIT 1) THEN
                        RAISE EXCEPTION 'AUDIT_ROLLBACK_REQUIRES_BACKUP_RESTORE';
                    END IF;
                END;
                $audit_down_guard$;
                """);

        migrationBuilder.DropTable(
            name: "audit_records");

        migrationBuilder.Sql(
            "DROP FUNCTION public.reject_audit_record_mutation();");
    }
}
