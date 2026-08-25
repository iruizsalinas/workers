internal sealed record CompilerOptions(
    string Project,
    string Output,
    string? Reference,
    IReadOnlyList<string> SourcePaths,
    IReadOnlyList<string> Symbols)
{
    private static readonly HashSet<string> KnownOptions =
    [
        "--project",
        "--output",
        "--sources",
        "--reference",
        "--define"
    ];

    public static CompilerOptions Parse(string[] args)
    {
        var values = ParseValues(args);
        var project = RequiredPath(values, "--project");
        var sourceList = OptionalPath(values, "--sources");
        var sources = sourceList is null
            ? Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(IsSource)
            : File.ReadLines(sourceList)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path, project));

        return new(
            project,
            RequiredPath(values, "--output"),
            OptionalPath(values, "--reference"),
            sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Value(values, "--define").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static IReadOnlyDictionary<string, string> ParseValues(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];
            if (!KnownOptions.Contains(name))
                throw new ArgumentException($"Unknown option '{name}'.");
            if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for {name}.");
            if (!values.TryAdd(name, args[index + 1]))
                throw new ArgumentException($"Option '{name}' was specified more than once.");
        }
        return values;
    }

    private static string RequiredPath(IReadOnlyDictionary<string, string> values, string name) =>
        Path.GetFullPath(values.TryGetValue(name, out var value) ? value : throw new ArgumentException($"Missing {name}."));

    private static string? OptionalPath(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) ? Path.GetFullPath(value) : null;

    private static string Value(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) ? value : "";
}
