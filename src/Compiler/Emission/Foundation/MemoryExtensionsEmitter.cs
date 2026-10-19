using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

// C# 14 first-class spans bind array.Contains(x), array.IndexOf(x) and similar calls to
// System.MemoryExtensions through an implicit array-to-span conversion. Arrays are JavaScript
// arrays (Uint8Array for bytes), so each span is its array; a null array is an empty span.
internal sealed partial class JavaScriptEmitter
{
    private static bool IsSpanType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition: { } definition }
        && definition.ToDisplayString() is "System.Span<T>" or "System.ReadOnlySpan<T>";

    private string? MemoryExtensionsInvocation(IInvocationOperation operation)
    {
        var method = operation.TargetMethod;
        if (method.ContainingType?.ToDisplayString() != "System.MemoryExtensions" || !method.IsGenericMethod) return null;
        var arguments = operation.Arguments.OrderBy(argument => argument.Parameter!.Ordinal).ToArray();
        if (arguments.Length == 0 || !IsSpanType(arguments[0].Parameter!.Type)) return null;
        // Only spans converted from arrays have a JavaScript representation here.
        string? SpanValue(IArgumentOperation argument) =>
            argument.Value is IConversionOperation { Operand.Type: IArrayTypeSymbol { Rank: 1 } } conversion
            && IsSpanType(conversion.Type)
                ? $"({Expression(conversion.Operand)} ?? [])"
                : null;
        if (SpanValue(arguments[0]) is not { } receiver) return null;
        var element = ResolveType(method.TypeArguments[0]);
        var source = operation.Syntax;
        var equals = EqualityFunction(element, source);
        string Call(string body, params string[] values)
        {
            string[] parameters = ["source", "other"];
            // Bind by parameter ordinal, while evaluating named arguments in their written order.
            var order = operation.Arguments.Select(argument => argument.Parameter!.Ordinal).ToArray();
            return $"(({string.Join(", ", order.Select(index => parameters[index]))}) => {{ const equals = {equals}; {body} }})"
                + $"({string.Join(", ", order.Select(index => values[index]))})";
        }
        var spanArgument = arguments.Length == 2 && IsSpanType(arguments[1].Parameter!.Type) ? SpanValue(arguments[1]) : null;
        // The single-element overloads take T itself, visible only on the unconstructed definition.
        var valueArgument = arguments.Length == 2 && SymbolEqualityComparer.Default.Equals(
            method.OriginalDefinition.Parameters[1].Type, method.OriginalDefinition.TypeParameters[0])
            ? ValueExpression(arguments[1].Value) : null;
        const string sequenceEqual = "if (source.length !== other.length) return false; " +
            "for (let index = 0; index < source.length; index++) if (!equals(source[index], other[index])) return false; return true;";
        const string startsWith = "if (other.length > source.length) return false; " +
            "for (let index = 0; index < other.length; index++) if (!equals(source[index], other[index])) return false; return true;";
        const string endsWith = "const offset = source.length - other.length; if (offset < 0) return false; " +
            "for (let index = 0; index < other.length; index++) if (!equals(source[offset + index], other[index])) return false; return true;";
        const string indexOf = "for (let index = 0; index < source.length; index++) if (equals(source[index], other)) return index; return -1;";
        const string lastIndexOf = "for (let index = source.length - 1; index >= 0; index--) if (equals(source[index], other)) return index; return -1;";
        return (method.Name, valueArgument, spanArgument) switch
        {
            ("Contains", { } value, _) => $"({Call(indexOf, receiver, value)} >= 0)",
            ("IndexOf", { } value, _) => Call(indexOf, receiver, value),
            ("LastIndexOf", { } value, _) => Call(lastIndexOf, receiver, value),
            ("SequenceEqual", _, { } other) => Call(sequenceEqual, receiver, other),
            ("StartsWith", _, { } other) => Call(startsWith, receiver, other),
            ("EndsWith", _, { } other) => Call(endsWith, receiver, other),
            ("StartsWith", { } value, _) => Call("return source.length !== 0 && equals(source[0], other);", receiver, value),
            ("EndsWith", { } value, _) => Call("return source.length !== 0 && equals(source[source.length - 1], other);", receiver, value),
            _ => throw UnsupportedSymbol(method, source)
        };
    }
}
