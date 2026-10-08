using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// Explicit conversions between the numeric types the compiler represents as JavaScript numbers,
// chars (one-character strings), enums and nullable values. Floating-point to integer conversions
// truncate and saturate, with NaN converting to zero, matching .NET on x64 since .NET 9.
internal sealed partial class JavaScriptEmitter
{
    private string Cast(CastExpressionSyntax cast)
    {
        if (_model.GetConstantValue(cast) is { HasValue: true } constant && constant.Value is not (float or long or ulong or decimal))
            return LiteralConstant(constant.Value, cast);
        if (_model.GetOperation(cast) is not IConversionOperation { Operand: var operand, Type: { } target } conversion
            || conversion.OperatorMethod is not null || conversion.IsChecked)
            throw Unsupported("WRK101", cast);
        var value = Expression(cast.Expression);
        var source = operand.Type;
        if (source is null) return value;
        if (conversion.Conversion.IsIdentity || source.IsValueType && target.SpecialType == SpecialType.System_Object
            || conversion.Conversion.IsImplicit && conversion.Conversion.IsReference)
            return value;

        var sourceNullable = source.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var targetNullable = target.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var sourceValue = UnwrapNullable(source);
        var targetValue = UnwrapNullable(target);
        var parameter = _names.Get($"cast:{cast.SyntaxTree.FilePath}:{cast.SpanStart}", "value");
        var converted = ConvertValue(sourceValue, targetValue, parameter, cast);
        if (sourceNullable && targetNullable)
            return $"(({parameter}) => {parameter} == null ? null : {converted})({value})";
        if (sourceNullable)
            return $"(({parameter}) => {{ if ({parameter} == null) throw new TypeError(\"Nullable object must have a value.\"); return {converted}; }})({value})";
        return converted == parameter ? value : $"(({parameter}) => {converted})({value})";
    }

    private string ConvertValue(ITypeSymbol source, ITypeSymbol target, string value, SyntaxNode node)
    {
        if (SymbolEqualityComparer.Default.Equals(source, target)) return value;
        var from = NumericKind(source);
        var to = NumericKind(target);
        if (from is null || to is null) throw Unsupported("WRK101", node);
        var number = from == SpecialType.System_Char ? $"{value}.charCodeAt(0)" : value;
        var floating = from is SpecialType.System_Single or SpecialType.System_Double;
        string Integer(double minimum, double maximum, string wrap) => floating
            ? $"(Number.isNaN({number}) ? 0 : Math.min(Math.max(Math.trunc({number}), {minimum}), {maximum}))"
            : wrap;
        return to switch
        {
            SpecialType.System_Double => number,
            SpecialType.System_Single => $"Math.fround({number})",
            SpecialType.System_Int32 => Integer(int.MinValue, int.MaxValue, $"({number} | 0)"),
            SpecialType.System_UInt32 => Integer(0, uint.MaxValue, $"({number} >>> 0)"),
            SpecialType.System_Int16 => Integer(short.MinValue, short.MaxValue, $"(({number} << 16) >> 16)"),
            SpecialType.System_UInt16 => Integer(0, ushort.MaxValue, $"({number} & 65535)"),
            SpecialType.System_SByte => Integer(sbyte.MinValue, sbyte.MaxValue, $"(({number} << 24) >> 24)"),
            SpecialType.System_Byte => Integer(0, byte.MaxValue, $"({number} & 255)"),
            SpecialType.System_Char => $"String.fromCharCode({Integer(0, ushort.MaxValue, $"({number} & 65535)")})",
            _ => throw Unsupported("WRK101", node)
        };
    }

    // Enums convert through their underlying type.
    private static SpecialType? NumericKind(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
            type = underlying;
        return type.SpecialType is SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Int32
            or SpecialType.System_UInt32 or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Char
            ? type.SpecialType
            : null;
    }
}
