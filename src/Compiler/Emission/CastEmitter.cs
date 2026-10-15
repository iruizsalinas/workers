using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

// Bound conversions between the numeric types the compiler represents as JavaScript numbers,
// chars (one-character strings), enums and nullable values. Floating-point to integer conversions
// truncate and saturate for Int32/UInt32, with NaN converting to zero, matching .NET on
// x64 since .NET 9. Narrower targets wrap the resulting integer to their bit width.
internal sealed partial class JavaScriptEmitter
{
    private string Conversion(IConversionOperation conversion)
    {
        var cast = conversion.Syntax;
        var operand = conversion.Operand;
        var target = conversion.Type ?? throw Unsupported("WRK101", cast);
        var nativeUnion = NativeUnionTypes.Contains(conversion.OperatorMethod?.ContainingType.ToDisplayString() ?? "");
        if (conversion.OperatorMethod is not null && !IsByteViewConversion(conversion) && !nativeUnion)
            throw UnsupportedSymbol(conversion.OperatorMethod, cast);
        var value = Expression(operand);
        var source = operand.Type;
        if (source is null) return value;
        if (IsByteViewConversion(conversion) || nativeUnion) return value;
        if (conversion.Conversion.IsIdentity || source.IsValueType && target.SpecialType == SpecialType.System_Object
            || conversion.Conversion.IsImplicit && conversion.Conversion.IsReference
            || conversion.IsImplicit && target.SpecialType == SpecialType.System_Object)
            return value;

        if (conversion.ConstantValue is { HasValue: true } constant && constant.Value is not decimal)
            return LiteralConstant(constant.Value, cast);
        var targetValue = UnwrapNullable(target);
        if (conversion.IsImplicit && operand.ConstantValue is { HasValue: true } supplied
            && supplied.Value is byte or sbyte or short or ushort or int or uint or long or ulong or double
            && targetValue.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double
            && targetValue.SpecialType != SpecialType.System_Single)
            return LiteralConstant(supplied.Value, cast);
        if (conversion.IsChecked) throw Unsupported("WRK101", cast);

        var sourceNullable = source.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var targetNullable = target.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var sourceValue = UnwrapNullable(source);
        // Character conversions read their operand once, so no capture wrapper is needed.
        if (!sourceNullable && sourceValue.SpecialType == SpecialType.System_Char)
            return ConvertValue(sourceValue, targetValue, Operand(operand), cast);
        var parameter = _names.Get($"cast:{cast.SyntaxTree.FilePath}:{cast.SpanStart}", "value");
        var widening = conversion.IsImplicit
            && (sourceValue.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
                && targetValue.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64
                || sourceValue.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64
                && targetValue.SpecialType == SpecialType.System_Double);
        var converted = widening
            ? parameter : ConvertValue(sourceValue, targetValue, parameter, cast);
        if (sourceNullable && targetNullable)
            return $"(({parameter}) => {parameter} == null ? null : {converted})({value})";
        if (sourceNullable)
            return $"(({parameter}) => {{ if ({parameter} == null) throw new TypeError(\"Nullable object must have a value.\"); return {converted}; }})({value})";
        return converted == parameter ? value : $"(({parameter}) => {converted})({value})";
    }

    // Types that stand for a JavaScript union, such as string | ReadableStream. Each member converts to the union
    // as itself, so the runtime receives the value unchanged.
    private static readonly HashSet<string> NativeUnionTypes =
    [
        "Workers.BodyInit", "Workers.ContainerInstanceType", "Workers.ContainerExecStdin", "Workers.ContainerExecPty",
        "Workers.EmailAddressInit", "Workers.EmailAttachmentContent"
    ];

    // Byte buffers use Uint8Array across the supported memory/span API boundaries.
    private static bool IsByteViewConversion(IConversionOperation conversion) =>
        conversion.IsImplicit && conversion.Operand.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
        && conversion.Type is INamedTypeSymbol { TypeArguments: [{ SpecialType: SpecialType.System_Byte }] } target
        && target.OriginalDefinition.ToDisplayString() is "System.ReadOnlyMemory<T>" or "System.Memory<T>"
            or "System.ReadOnlySpan<T>" or "System.Span<T>";

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
        var narrow = floating ? Integer(int.MinValue, int.MaxValue, number) : number;
        return to switch
        {
            SpecialType.System_Double => number,
            SpecialType.System_Single => $"Math.fround({number})",
            SpecialType.System_Int32 => Integer(int.MinValue, int.MaxValue, $"({number} | 0)"),
            SpecialType.System_UInt32 => Integer(0, uint.MaxValue, $"({number} >>> 0)"),
            SpecialType.System_Int16 => $"(({narrow} << 16) >> 16)",
            SpecialType.System_UInt16 => $"({narrow} & 65535)",
            SpecialType.System_SByte => $"(({narrow} << 24) >> 24)",
            SpecialType.System_Byte => $"({narrow} & 255)",
            SpecialType.System_Char => $"String.fromCharCode({narrow} & 65535)",
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
