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

    // Nonzero while emitting FormattableString.Invariant or string.Create(CultureInfo.InvariantCulture, ...),
    // where culture-sensitive format specifiers produce invariant output.
    private int _invariantFormatting;

    private string InterpolationText(InterpolationSyntax item)
    {
        var text = item.FormatClause is { } clause ? FormattedInterpolation(item, clause.FormatStringToken.ValueText) : InterpolationValue(item);
        if (item.AlignmentClause is null) return text;
        if (_model.GetConstantValue(item.AlignmentClause.Value) is not { HasValue: true, Value: int width })
            throw Unsupported("WRK108", item.AlignmentClause);
        return width >= 0 ? $"String({text}).padStart({width})" : $"String({text}).padEnd({-width})";
    }

    private string FormattedInterpolation(InterpolationSyntax item, string format)
    {
        var type = _model.GetTypeInfo(item.Expression).Type;
        if (type is null) throw Unsupported("WRK108", item);
        var nullable = type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var parameter = nullable ? "$workers$value" : Expression(item.Expression);
        var formatted = DateFormatExpression(type, format, parameter) is { } date
                        && (_invariantFormatting > 0 || IsCultureInvariantDateFormat(format))
            ? date
            : _invariantFormatting > 0 ? NumericFormatExpression(type, format, parameter) : null;
        if (formatted is null)
            throw new NotSupportedException(_invariantFormatting > 0
                ? $"WRK108: The format '{format}' is not supported for '{type.ToDisplayString()}'."
                : $"WRK108: The format '{format}' depends on the current culture; use FormattableString.Invariant($\"...\") or string.Create(CultureInfo.InvariantCulture, $\"...\").");
        return nullable
            ? $"(({parameter}) => {parameter} == null ? \"\" : {formatted})({Expression(item.Expression)})"
            : formatted;
    }

    private string InvariantInterpolation(ExpressionSyntax interpolation)
    {
        _invariantFormatting++;
        try
        {
            return Expression(interpolation);
        }
        finally
        {
            _invariantFormatting--;
        }
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
        if (underlying?.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
        {
            var single = underlying.SpecialType == SpecialType.System_Single ? ", true" : "";
            var helper = _helpers.Require(JavaScriptHelper.NumberText);
            return mayBeNull
                ? $"(($workers$value) => $workers$value == null ? \"\" : {helper}($workers$value{single}))({expression})"
                : $"{helper}({expression}{single})";
        }
        if (underlying?.ToDisplayString() == "System.Text.StringBuilder")
        {
            _helpers.Require(JavaScriptHelper.StringBuilder);
            return $"(($workers$value) => $workers$value == null ? \"\" : {_helpers.Name("stringBuilderText")}($workers$value))({expression})";
        }
        if (IsTextEnum(underlying))
            return mayBeNull
                ? $"(($workers$value) => $workers$value == null ? \"\" : {EnumText(underlying!, "$workers$value")})({expression})"
                : EnumText(underlying!, expression);
        if (underlying is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
            return $"(($workers$value) => $workers$value == null ? \"\" : {RecordText(record, "$workers$value", item)})({expression})";
        if (underlying?.SpecialType is SpecialType.System_String or SpecialType.System_Char
            or >= SpecialType.System_SByte and <= SpecialType.System_Double
            || underlying?.ToDisplayString() == "System.Guid")
            return mayBeNull ? $"({expression}) ?? \"\"" : expression;
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
