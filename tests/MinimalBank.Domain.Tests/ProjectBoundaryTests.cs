using Xunit;

namespace MinimalBank.Domain.Tests;

public class ProjectBoundaryTests
{
    [Fact]
    public void Domain_Assembly_Loads()
    {
        var assembly = typeof(MinimalBank.Domain.Placeholder).Assembly;
        Assert.NotNull(assembly);
        Assert.Contains("MinimalBank.Domain", assembly.FullName);
    }

    [Fact]
    public void Application_Assembly_Loads()
    {
        var assembly = typeof(MinimalBank.Application.Placeholder).Assembly;
        Assert.NotNull(assembly);
        Assert.Contains("MinimalBank.Application", assembly.FullName);
    }

    [Fact]
    public void Infrastructure_Assembly_Loads()
    {
        var assembly = typeof(MinimalBank.Infrastructure.Placeholder).Assembly;
        Assert.NotNull(assembly);
        Assert.Contains("MinimalBank.Infrastructure", assembly.FullName);
    }
}
