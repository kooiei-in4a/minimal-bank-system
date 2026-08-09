using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalBankSystem.Infrastructure.Migrations;

/// <summary>
/// Empty foundation baseline (Issue #42 section 8.5).
/// </summary>
/// <remarks>
/// FND-04 owns migration machinery, not business schema, so this migration intentionally creates
/// no table, sequence, trigger or constraint. Applying it proves the machinery end to end: EF Core
/// creates <c>public.__EFMigrationsHistory</c> and records this migration id. The first non-empty
/// migration belongs to the schema-owning Issue that introduces the entities.
/// </remarks>
public partial class InitialFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
