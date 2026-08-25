internal sealed record CompilerOptions(
    string Project,
    string Output,
    string? Reference,
    IReadOnlyList<string> SourcePaths,
    IReadOnlyList<string> Symbols)
{
    public static CompilerOptions Parse(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var sourceList = OptionalPath(args, "--sources-file");
        var sources = sourceList is null
            ? Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(IsSource)
            : File.ReadLines(sourceList)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path, project));

        return new(
            project,
            RequiredPath(args, "--output"),
            OptionalPath(args, "--workers-reference"),
            sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            (Value(args, "--define") ?? "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string RequiredPath(string[] args, string name) =>
        Path.GetFullPath(Value(args, name) ?? throw new ArgumentException($"Missing {name}."));

    private static string? OptionalPath(string[] args, string name) =>
        Value(args, name) is { } value ? Path.GetFullPath(value) : null;

    private static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index < 0 || index + 1 == args.Length ? null : args[index + 1];
    }
}
