internal sealed record CompilerOptions(string Project, string Output, string ResponseFile)
{
    private static readonly HashSet<string> KnownOptions =
    [
        "--project",
        "--output",
        "--rsp"
    ];

    public static CompilerOptions Parse(string[] args)
    {
        var values = ParseValues(args);
        return new(
            RequiredPath(values, "--project"),
            RequiredPath(values, "--output"),
            RequiredPath(values, "--rsp"));
    }

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
}
