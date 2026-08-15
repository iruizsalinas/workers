using System.Text;
using Microsoft.CodeAnalysis;

internal sealed class GeneratedNameAllocator
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _allocated = new(StringComparer.Ordinal);

    public string Get(string key, string? preferredName = null)
    {
        if (_names.TryGetValue(key, out var existing))
            return existing;

        var stem = "$workers$" + Sanitize(preferredName ?? key);
        var candidate = stem;
        for (var suffix = 2; !_allocated.Add(candidate); suffix++)
            candidate = stem + "$" + suffix;

        _names.Add(key, candidate);
        return candidate;
    }

    public string ForMethod(IMethodSymbol method, int ordinal)
    {
        var owner = method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return Get($"method:{method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}", $"cs${owner}${method.Name}${ordinal}");
    }

    private static string Sanitize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
            result.Append(char.IsLetterOrDigit(character) || character is '$' or '_' ? character : '_');
        return result.ToString();
    }
}
