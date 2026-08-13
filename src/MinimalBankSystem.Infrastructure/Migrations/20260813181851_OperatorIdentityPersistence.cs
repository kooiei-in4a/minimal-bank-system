using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <inheritdoc />
public partial class OperatorIdentityPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Operators",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AuthorizationStateVersion = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                ConcurrencyStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Operators", x => x.Id);
                table.CheckConstraint("CK_Operators_AuthorizationStateVersion", "\"AuthorizationStateVersion\" > 0");
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
