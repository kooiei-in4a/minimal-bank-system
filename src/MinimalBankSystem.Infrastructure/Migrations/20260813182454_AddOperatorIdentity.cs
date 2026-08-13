using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddOperatorIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Operators",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserName = table.Column<string>(type: "text", nullable: false),
                NormalizedUserName = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                SecurityStamp = table.Column<string>(type: "text", nullable: false),
                Role = table.Column<string>(type: "text", nullable: false),
                State = table.Column<string>(type: "text", nullable: false),
                AuthorizationStateVersion = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Operators", x => x.Id);
                table.CheckConstraint("CK_Operators_Role", "\"Role\" IN ('administrator', 'teller', 'viewer')");
                table.CheckConstraint("CK_Operators_State", "\"State\" IN ('active', 'disabled')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Operators_NormalizedUserName",
            table: "Operators",
            column: "NormalizedUserName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Operators");
    }
}
