using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <summary>Operator identity persistence owned by WP2-ID-01.</summary>
public partial class AddOperatorIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "operators",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_name = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                normalized_user_name = table.Column<string>(type: "text", maxLength: 256, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                security_stamp = table.Column<string>(type: "text", nullable: false),
                state = table.Column<string>(type: "text", nullable: false),
                fixed_role = table.Column<string>(type: "text", nullable: false),
                authorization_state_version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operators", x => x.id);
                table.CheckConstraint("ck_operators_fixed_role", "fixed_role IN ('administrator', 'teller', 'viewer')");
                table.CheckConstraint("ck_operators_state", "state IN ('active', 'disabled')");
            });

        migrationBuilder.CreateIndex(
            name: "ix_operators_normalized_user_name",
            table: "operators",
            column: "normalized_user_name",
            unique: true);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "operators");
    }
}
