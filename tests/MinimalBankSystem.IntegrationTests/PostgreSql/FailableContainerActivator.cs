using System.Reflection;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal static class FailableContainerActivator
{
    private static readonly FieldInfo ClientField =
        typeof(DockerContainer).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "DockerContainer._client private field was not found. The Testcontainers version this fixture was written against (4.13.0) no longer matches the runtime reference.");

    private static readonly FieldInfo DisposedField =
        typeof(Resource).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Resource._disposed private field was not found. The Testcontainers version this fixture was written against (4.13.0) no longer matches the runtime reference.");

    private static readonly FieldInfo ContainerInspectField =
        typeof(DockerContainer).GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "DockerContainer._container private field was not found. The Testcontainers version this fixture was written against (4.13.0) no longer matches the runtime reference.");

    public static void ArmPostgreSqlContainerDisposeFailure(PostgreSqlContainer container)
    {
        DisposedField.SetValue(container, 0);
        ClientField.SetValue(container, null);
        ContainerInspectField.SetValue(container, null);
    }
}
