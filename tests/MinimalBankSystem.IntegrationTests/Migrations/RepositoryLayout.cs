namespace MinimalBankSystem.IntegrationTests.Migrations;

internal static class RepositoryLayout
{
    public static string ResolveProjectBinary(string projectName)
    {
        DirectoryInfo? repositoryRoot = FindRepositoryRoot();

        if (repositoryRoot is null)
        {
            throw new InvalidOperationException("Could not locate the repository root.");
        }

        string dllPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            projectName,
            "bin",
            GetConfiguration(),
            "net10.0",
            $"{projectName}.dll");

        Assert.True(
            File.Exists(dllPath),
            $"Expected the built '{projectName}' at '{dllPath}'. The solution must be built before running these tests.");

        return dllPath;
    }

    private static DirectoryInfo? FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory;
    }

    private static string GetConfiguration()
    {
        string[] segments = AppContext.BaseDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar);

        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index] == "bin")
            {
                return segments[index + 1];
            }
        }

        return "Debug";
    }
}
