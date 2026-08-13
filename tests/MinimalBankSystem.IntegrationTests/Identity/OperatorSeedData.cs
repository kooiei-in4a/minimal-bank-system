using MinimalBankSystem.Infrastructure.Identity;

namespace MinimalBankSystem.IntegrationTests.Identity;

/// <summary>
/// Integration-test-only Operator seed data (Issue #165 verification requirement #6: "integration-
/// test-only Operator seed data is owned here and is unreachable from production paths").
/// </summary>
/// <remarks>
/// This type lives exclusively in the <c>MinimalBankSystem.IntegrationTests</c> assembly, which no
/// production assembly (Api, Infrastructure, Migrator) references. Unreachability therefore holds
/// by compile-time construction, not only by convention; <see cref="BoundaryAssemblyTests"/> pins
/// the guarantee with an explicit assembly-reference scan.
/// </remarks>
internal static class OperatorSeedData
{
    internal const string AdministratorUserName = "wp2-id01-seed-administrator";
    internal const string TellerUserName = "wp2-id01-seed-teller";
    internal const string ViewerUserName = "wp2-id01-seed-viewer";

    /// <summary>Test-only plaintext credential shared by the seeded Operators. Never used outside this assembly.</summary>
    internal const string SeedPassword = "Wp2-Id01-Seed-P@ssw0rd!";

    internal static Operator CreateAdministrator(TimeProvider timeProvider) =>
        OperatorFactory.Create(timeProvider, AdministratorUserName, SeedPassword, OperatorRole.Administrator);

    internal static Operator CreateTeller(TimeProvider timeProvider) =>
        OperatorFactory.Create(timeProvider, TellerUserName, SeedPassword, OperatorRole.Teller);

    internal static Operator CreateViewer(TimeProvider timeProvider) =>
        OperatorFactory.Create(timeProvider, ViewerUserName, SeedPassword, OperatorRole.Viewer);
}
