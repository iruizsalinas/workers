using System.Text.RegularExpressions;
using Xunit;

namespace Workers.Tests;

public sealed class PackageSafetyTests
{
    [Fact]
    public void PackageEnablesUnsafeOnlyForJavaScriptInterop()
    {
        var sourceRoot = Path.Combine(RepositoryRoot(), "src", "Workers");
        var projectFile = Path.Combine(RepositoryRoot(), "src", "Workers", "Workers.csproj");
        var project = File.ReadAllText(projectFile);
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Contains("<AllowUnsafeBlocks>true</AllowUnsafeBlocks>", project, StringComparison.Ordinal);
        Assert.Contains("JSExport", source, StringComparison.Ordinal);
        Assert.Contains("JSImport", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceDoesNotUseUnsafeCode()
    {
        var sourceRoot = Path.Combine(RepositoryRoot(), "src", "Workers");
        var unsafeConstruct = new Regex(@"\bunsafe\s*(\{|[A-Za-z_])", RegexOptions.CultureInvariant);

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var source = File.ReadAllText(file);

            Assert.False(unsafeConstruct.IsMatch(source), $"Unexpected handwritten unsafe construct in {relativePath}.");
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Workers.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the Workers repository root.");
    }
}
