using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Literal(LiteralExpressionSyntax literal)
    {
        if (literal.IsKind(SyntaxKind.DefaultLiteralExpression)) return DefaultValue(literal);
        if (literal.IsKind(SyntaxKind.NullLiteralExpression)) return "null";
        if (literal.IsKind(SyntaxKind.TrueLiteralExpression)) return "true";
        if (literal.IsKind(SyntaxKind.FalseLiteralExpression)) return "false";
        var type = _model.GetTypeInfo(literal).Type?.SpecialType ?? SpecialType.None;
        if (type is SpecialType.System_String or SpecialType.System_Char) return JsonSerializer.Serialize(literal.Token.ValueText);
        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal) throw Unsupported("WRK108", literal);
        if (literal.Token.Value is IFormattable value)
            return value.ToString(null, CultureInfo.InvariantCulture) switch
            {
                "NaN" => "Number.NaN",
                "Infinity" => "Number.POSITIVE_INFINITY",
                "-Infinity" => "Number.NEGATIVE_INFINITY",
                var text => text
            };
        throw Unsupported("WRK101", literal);
    }

    private string InterpolatedPart(InterpolatedStringContentSyntax value) => value switch
    {
        InterpolatedStringTextSyntax text => EscapeTemplateText(text.TextToken.ValueText),
        InterpolationSyntax { AlignmentClause: not null } item => throw Unsupported("WRK108", item),
        InterpolationSyntax { FormatClause.FormatStringToken.ValueText: "O" or "o" } item =>
            DateTimeRoundTripInterpolation(item),
        InterpolationSyntax { FormatClause: not null } item => throw Unsupported("WRK108", item),
        InterpolationSyntax item => "${" + InterpolationValue(item) + "}",
        _ => throw Unsupported("WRK108", value)
    };

    private string DateTimeRoundTripInterpolation(InterpolationSyntax item)
    {
        var type = _model.GetTypeInfo(item.Expression).Type?.ToDisplayString();
        return type switch
        {
            "System.DateTime" => "${" + DateTimeRoundTrip(Expression(item.Expression), includeOffset: false) + "}",
            "System.DateTimeOffset" => "${" + DateTimeRoundTrip(Expression(item.Expression)) + "}",
            _ => throw Unsupported("WRK108", item)
        };
    }

    private string InterpolationValue(InterpolationSyntax item)
    {
        var expression = Expression(item.Expression);
        var type = _model.GetTypeInfo(item.Expression).Type;
        var underlying = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        if (underlying?.SpecialType == SpecialType.System_Boolean)
            return $"(($workers$value) => $workers$value == null ? \"\" : ($workers$value ? \"True\" : \"False\"))({expression})";
        if (underlying?.SpecialType is SpecialType.System_String or SpecialType.System_Char
            or >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return $"{expression} ?? \"\"";
        if (underlying?.ToDisplayString() == "System.Guid")
            return $"{expression} ?? \"\"";
        throw Unsupported("WRK108", item);
    }

    private static string EscapeTemplateText(string value) => value.Replace("{{", "{", StringComparison.Ordinal)
        .Replace("}}", "}", StringComparison.Ordinal).Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal).Replace("${", "\\${", StringComparison.Ordinal);

    private static string DateTimeRoundTrip(string value, bool includeOffset = true) => includeOffset
        ? $"new Date({value}).toISOString().replace(/(\\.\\d{{3}})Z$/, \"$1\" + \"0000+00:00\")"
        : $"new Date({value}).toISOString().replace(/(\\.\\d{{3}})Z$/, \"$1\" + \"0000\")";

    private string Element(CollectionElementSyntax value) => value switch
    {
        ExpressionElementSyntax expression => Expression(expression.Expression),
        SpreadElementSyntax spread => "..." + Expression(spread.Expression),
        _ => throw Unsupported("WRK102", value)
    };
}
