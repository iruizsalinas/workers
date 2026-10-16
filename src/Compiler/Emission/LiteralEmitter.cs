using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    // System.Text.Json replaces unpaired UTF-16 surrogates. JavaScript strings can retain them,
    // which matters when native encoders and DigestStream decide how to encode each chunk.
    private static string JavaScriptStringLiteral(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
                continue;
            }
            if (char.IsSurrogate(text[index]))
                return "\"" + string.Concat(text.Select(character => "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture))) + "\"";
        }
        return JsonSerializer.Serialize(text);
    }

    private string InterpolatedPart(IInterpolatedStringContentOperation value) => value switch
    {
        IInterpolatedStringTextOperation text => EscapeTemplateText((string)text.Text.ConstantValue.Value!),
        IInterpolationOperation item => "${" + InterpolationText(item) + "}",
        _ => throw Unsupported("WRK108", value.Syntax)
    };

    private string InterpolationText(IInterpolationOperation item)
    {
        if (item.FormatString is { } format)
            throw new NotSupportedException($"WRK108: The format '{format.ConstantValue.Value}' is outside the Workers profile; "
                + "format numbers with Numbers.ToFixed or Numbers.ToPrecision and dates with Date.ToIsoString.");
        var text = InterpolationValue(item);
        if (item.Alignment is null) return text;
        if (item.Alignment.ConstantValue is not { HasValue: true, Value: int width })
            throw Unsupported("WRK108", item.Alignment.Syntax);
        return width >= 0 ? $"String({text}).padStart({width})" : $"String({text}).padEnd({-width})";
    }


    private string InterpolationValue(IInterpolationOperation item)
    {
        return StringConcatOperand(item.Expression, Expression(item.Expression), item.Syntax, interpolation: true);
    }

    private static IOperation Unbox(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true, Type.SpecialType: SpecialType.System_Object } conversion)
            operation = conversion.Operand;
        return operation;
    }

    private static string EscapeTemplateText(string value) => value.Replace("{{", "{", StringComparison.Ordinal)
        .Replace("}}", "}", StringComparison.Ordinal).Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal).Replace("${", "\\${", StringComparison.Ordinal);

}
