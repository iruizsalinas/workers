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
        // 64-bit integers are JavaScript numbers, so only literals in the safe integer range are exact.
        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && literal.Token.Value is long or ulong
            && Convert.ToDecimal(literal.Token.Value, CultureInfo.InvariantCulture) is <= 9007199254740991m and >= -9007199254740991m)
            return Convert.ToString(literal.Token.Value, CultureInfo.InvariantCulture)!;
        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal) throw Unsupported("WRK108", literal);
        // A float value is the nearest single-precision number, so emit that exact double. This also
        // covers integer constants implicitly converted to float.
        if (_model.GetTypeInfo(literal).ConvertedType?.SpecialType == SpecialType.System_Single
            && literal.Token.Value is IConvertible numeric and not string and not char)
        {
            var single = (double)Convert.ToSingle(numeric, CultureInfo.InvariantCulture);
            if (double.IsFinite(single))
                return single.ToString("R", CultureInfo.InvariantCulture);
        }
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
        InterpolationSyntax item => "${" + InterpolationText(item) + "}",
        _ => throw Unsupported("WRK108", value)
    };

    private string InterpolationText(InterpolationSyntax item)
    {
        if (item.FormatClause is { } clause)
            throw new NotSupportedException($"WRK108: The format '{clause.FormatStringToken.ValueText}' is outside the Workers profile; "
                + "format numbers with Numbers.ToFixed or Numbers.ToPrecision and dates with Date.ToIsoString.");
        var text = InterpolationValue(item);
        if (item.AlignmentClause is null) return text;
        if (_model.GetConstantValue(item.AlignmentClause.Value) is not { HasValue: true, Value: int width })
            throw Unsupported("WRK108", item.AlignmentClause);
        return width >= 0 ? $"String({text}).padStart({width})" : $"String({text}).padEnd({-width})";
    }


    private string InterpolationValue(InterpolationSyntax item)
    {
        var expression = Expression(item.Expression);
        var type = _model.GetTypeInfo(item.Expression).Type;
        var underlying = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        var mayBeNull = type is null || !type.IsValueType || !SymbolEqualityComparer.Default.Equals(type, underlying);
        if (underlying?.SpecialType == SpecialType.System_Boolean)
            return mayBeNull
                ? $"(($workers$value) => $workers$value == null ? \"\" : ($workers$value ? \"True\" : \"False\"))({expression})"
                : $"({expression} ? \"True\" : \"False\")";
        if (underlying?.SpecialType is SpecialType.System_String or SpecialType.System_Char
            or >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return mayBeNull ? $"({expression}) ?? \"\"" : expression;
        throw Unsupported("WRK108", item);
    }

    private static string EscapeTemplateText(string value) => value.Replace("{{", "{", StringComparison.Ordinal)
        .Replace("}}", "}", StringComparison.Ordinal).Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal).Replace("${", "\\${", StringComparison.Ordinal);

    private string Element(CollectionElementSyntax value) => value switch
    {
        ExpressionElementSyntax expression => Expression(expression.Expression),
        SpreadElementSyntax spread => "..." + Expression(spread.Expression),
        _ => throw Unsupported("WRK102", value)
    };
}
