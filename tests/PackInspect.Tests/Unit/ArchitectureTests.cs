using System.Reflection;

namespace PackInspect.Tests.Unit;

/// <summary>
/// Guards the dependency rules in docs/ARCHITECTURE-ESSENTIALS.md, so a wrong
/// project reference fails the build pipeline instead of going unnoticed.
/// </summary>
public class ArchitectureTests
{
    private const string SolutionPrefix = "PackInspect.";

    [Theory]
    [InlineData("PackInspect.Core")]
    [InlineData("PackInspect.Simulator")]
    [InlineData("PackInspect.LegacyDriver")]
    [InlineData("PackInspect.Devices", "PackInspect.Core", "PackInspect.LegacyDriver")]
    [InlineData("PackInspect.Data", "PackInspect.Core")]
    public void Project_references_only_the_allowed_projects(string assemblyName, params string[] allowed)
    {
        var referenced = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(SolutionPrefix, StringComparison.Ordinal));

        Assert.All(referenced, name => Assert.Contains(name, allowed));
    }

    [Fact]
    public void Core_does_not_reference_wpf()
    {
        string[] wpfAssemblies = ["PresentationFramework", "PresentationCore", "WindowsBase"];

        var referenced = Assembly.Load("PackInspect.Core")
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!);

        Assert.DoesNotContain(referenced, name => wpfAssemblies.Contains(name));
    }
}
