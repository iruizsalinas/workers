using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AspNetJavaScript
{
    public static string String(string value) => JsonSerializer.Serialize(value);

    public static string Literal(LiteralExpressionSyntax literal) => literal.Token.Value switch
    {
        null => "null",
        string value => String(value),
        char value => String(value.ToString()),
        bool value => value ? "true" : "false",
        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
        _ => throw AspNetDiagnostic.Unsupported("WRK210", literal, "This literal is not supported in ASP.NET handlers.")
    };

    public static string Property(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
