using System.Text.Json;

internal sealed record NpmPackage(string Name, string InstallSpec, Version Minimum);

internal sealed record NpmRequirement(NpmPackage Package, string? InstalledVersion)
{
    public string Problem => InstalledVersion is null
        ? $"'{Package.Name}' is not installed"
        : $"'{Package.Name}' {InstalledVersion} is older than the supported {Package.Minimum}";
}

// Modules imported by generated Workers that Wrangler resolves from node_modules when it bundles.
// Minimum versions are the first releases documented to work on Workers.
internal static class NpmDependencies
{
    private static readonly IReadOnlyDictionary<string, NpmPackage> Packages = new Dictionary<string, NpmPackage>(StringComparer.Ordinal)
    {
        ["pg"] = new("pg", "pg@^8.16.3", new(8, 16, 3)),
        ["mysql2/promise"] = new("mysql2", "mysql2@^3.13.0", new(3, 13, 0)),
        ["mongodb"] = new("mongodb", "mongodb@^7.0.0", new(6, 15, 0))
    };

    // Resolves packages the way the bundler does: from the module's directory up through each
    // ancestor's node_modules.
    public static IReadOnlyList<NpmRequirement> Unsatisfied(IEnumerable<string> modules, string outputDirectory) =>
        modules.Select(module => Packages.GetValueOrDefault(module))
            .OfType<NpmPackage>()
            .DistinctBy(package => package.Name)
            .Select(package => new NpmRequirement(package, InstalledVersion(package.Name, outputDirectory)))
            .Where(requirement => requirement.InstalledVersion is null
                || !Version.TryParse(requirement.InstalledVersion.Split('-', '+')[0], out var version)
                || version < requirement.Package.Minimum)
            .ToArray();

    // npm installs into the nearest package.json above the Worker, or into the project when the Worker
    // is inside it. Anywhere else the packages would not resolve from the Worker.
    public static string? InstallDirectory(string project, string outputDirectory)
    {
        for (var current = new DirectoryInfo(outputDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "package.json")))
                return current.FullName;
        var relative = Path.GetRelativePath(project, outputDirectory);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            ? null
            : project;
    }

    private static string? InstalledVersion(string name, string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var manifest = Path.Combine(current.FullName, "node_modules", name, "package.json");
            if (!File.Exists(manifest)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.TryGetProperty("version", out var version) ? version.GetString() : null;
        }
        return null;
    }
}
