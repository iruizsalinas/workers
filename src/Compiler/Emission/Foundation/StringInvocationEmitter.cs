using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// String members map to the JavaScript string methods with the same purpose and keep JavaScript
// behavior: trimming, casing and searching follow the JavaScript definitions, and ranges clamp.
internal sealed partial class JavaScriptEmitter
{
    private string StringInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("Trim", 0) => $"{receiver}.trim()",
            ("TrimStart", 0) => $"{receiver}.trimStart()",
            ("TrimEnd", 0) => $"{receiver}.trimEnd()",
            ("ToLowerInvariant", 0) => $"{receiver}.toLowerCase()",
            ("ToUpperInvariant", 0) => $"{receiver}.toUpperCase()",
            ("Contains", 1) => $"{receiver}.includes({arguments[0]})",
            ("StartsWith", 1) => $"{receiver}.startsWith({arguments[0]})",
            ("EndsWith", 1) => $"{receiver}.endsWith({arguments[0]})",
            ("IndexOf", 1) => $"{receiver}.indexOf({arguments[0]})",
            ("IndexOf", 2) when method?.Parameters[1].Type.SpecialType == SpecialType.System_Int32 =>
                $"{receiver}.indexOf({arguments[0]}, {arguments[1]})",
            ("LastIndexOf", 1) => $"{receiver}.lastIndexOf({arguments[0]})",
            ("Contains" or "StartsWith" or "EndsWith" or "IndexOf" or "LastIndexOf" or "Equals", 2)
                when StringComparisonMode(source, method) is { } ignoreCase =>
                CompareStrings(name, receiver, arguments[0], ignoreCase),
            ("Equals", 1) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"({receiver} === {arguments[0]})",
            ("Substring", 1) => $"{receiver}.slice({arguments[0]})",
            ("Substring", 2) => $"{receiver}.slice({arguments[0]}).slice(0, {arguments[1]})",
            ("Replace", 2) => $"{receiver}.replaceAll({arguments[0]}, {arguments[1]})",
            ("Replace", 3) when StringComparisonMode(source, method) == false =>
                $"{receiver}.replaceAll({arguments[0]}, {arguments[1]})",
            ("Remove", 1) => $"{receiver}.slice(0, {arguments[0]})",
            ("Remove", 2) => $"((text, start, count) => text.slice(0, start) + text.slice(start + count))({receiver}, {arguments[0]}, {arguments[1]})",
            ("Insert", 2) => $"((text, index, value) => text.slice(0, index) + value + text.slice(index))({receiver}, {arguments[0]}, {arguments[1]})",
            ("PadLeft", 1 or 2) => $"{receiver}.padStart({string.Join(", ", arguments)})",
            ("PadRight", 1 or 2) => $"{receiver}.padEnd({string.Join(", ", arguments)})",
            ("ToCharArray", 0) => $"{receiver}.split(\"\")",
            ("Split", 1) when method?.Parameters[0].Type.SpecialType is SpecialType.System_Char or SpecialType.System_String =>
                $"{receiver}.split({arguments[0]})",
            ("Split", 2) when method?.Parameters is [{ Type.SpecialType: SpecialType.System_Char or SpecialType.System_String }, { Type.TypeKind: TypeKind.Enum }]
                && SplitOptions(source) is { } options =>
                StringSplit(receiver, arguments[0], options),
            _ => throw UnsupportedSymbol(method, source)
        };

    private string StringStaticInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string name,
        string[] arguments) => name switch
        {
            "IsNullOrEmpty" when arguments.Length == 1 => $"(value => value == null || value === \"\")({arguments[0]})",
            "IsNullOrWhiteSpace" when arguments.Length == 1 => $"(value => value == null || value.trim() === \"\")({arguments[0]})",
            "Join" => StringJoin(source, method, arguments),
            "Concat" => StringConcat(source, method, arguments),
            "Equals" when arguments.Length == 2 => $"({arguments[0]} === {arguments[1]})",
            "Equals" when arguments.Length == 3 && StringComparisonMode(source, method) is { } ignoreCase =>
                ignoreCase ? $"({arguments[0]}?.toUpperCase() === {arguments[1]}?.toUpperCase())" : $"({arguments[0]} === {arguments[1]})",
            _ => throw UnsupportedSymbol(method, source)
        };

    // Ordinal comparisons are the native ones; ignoring case compares the upper-case forms.
    private static string CompareStrings(string name, string receiver, string value, bool ignoreCase)
    {
        var (left, right) = ignoreCase ? ($"{receiver}.toUpperCase()", $"{value}.toUpperCase()") : (receiver, value);
        return name switch
        {
            "Equals" => ignoreCase ? $"({left} === {value}?.toUpperCase())" : $"({receiver} === {value})",
            "Contains" => $"{left}.includes({right})",
            "StartsWith" => $"{left}.startsWith({right})",
            "EndsWith" => $"{left}.endsWith({right})",
            "IndexOf" => $"{left}.indexOf({right})",
            _ => $"{left}.lastIndexOf({right})"
        };
    }

    // StringSplitOptions: RemoveEmptyEntries = 1, TrimEntries = 2. Options must be constant.
    private static string StringSplit(string receiver, string separator, int options)
    {
        var split = $"{receiver}.split({separator})";
        if ((options & 2) != 0) split += ".map(value => value.trim())";
        if ((options & 1) != 0) split += ".filter(value => value.length !== 0)";
        return split;
    }

    private int? SplitOptions(InvocationExpressionSyntax source) =>
        _model.GetConstantValue(source.ArgumentList.Arguments[1].Expression) is { HasValue: true, Value: int value } && (value & ~3) == 0
            ? value : null;

    private string StringJoin(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        if (method.Parameters.Length != 2 || method.TypeArguments.Length != 0
            || method.Parameters[0].Type.SpecialType is not (SpecialType.System_String or SpecialType.System_Char))
            throw UnsupportedSymbol(method, source);
        var values = StringSequenceArgument(method.Parameters[1].Type, method.Parameters[1].IsParams, arguments[1..]);
        if (values is null) throw UnsupportedSymbol(method, source);
        return $"Array.from({values}).join({arguments[0]})";
    }

    private string StringConcat(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        if (method.TypeArguments.Length != 0)
            throw UnsupportedSymbol(method, source);
        if (method.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String))
            return $"[{string.Join(", ", arguments)}].join(\"\")";
        if (method.Parameters is [{ Type: { } type, IsParams: var isParams }]
            && StringSequenceArgument(type, isParams, arguments) is { } values)
            return $"Array.from({values}).join(\"\")";
        throw UnsupportedSymbol(method, source);
    }

    private static string? StringSequenceArgument(ITypeSymbol type, bool isParams, IReadOnlyList<string> arguments)
    {
        var isStringArray = type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String };
        var isStringSpan = type is INamedTypeSymbol
        {
            OriginalDefinition: { } definition,
            TypeArguments: [{ SpecialType: SpecialType.System_String }]
        } && definition.ToDisplayString() == "System.ReadOnlySpan<T>";
        var isStringEnumerable = type is INamedTypeSymbol named && named.TypeArguments.Length == 1
            && named.TypeArguments[0].SpecialType == SpecialType.System_String
            && (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>"
                || named.AllInterfaces.Any(item =>
                    item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>"));
        if (!isStringArray && !isStringSpan && !isStringEnumerable) return null;
        return isParams && arguments.Count != 1 ? $"[{string.Join(", ", arguments)}]" : arguments[0];
    }

    private static bool? StringComparisonMode(InvocationExpressionSyntax source, IMethodSymbol? method) =>
        InvocationArgument(source, method, "comparisonType") is MemberAccessExpressionSyntax comparison
        && comparison.Name.Identifier.ValueText is "Ordinal" or "OrdinalIgnoreCase"
            ? comparison.Name.Identifier.ValueText == "OrdinalIgnoreCase"
            : null;

    private static ExpressionSyntax? InvocationArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string parameterName)
    {
        if (method is null)
            return null;
        var ordinal = method.Parameters.FirstOrDefault(parameter => parameter.Name == parameterName)?.Ordinal;
        if (ordinal is null)
            return null;
        return invocation.ArgumentList.Arguments.Select((argument, position) => new
            {
                Argument = argument,
                Parameter = InvocationParameter(method, argument, position)
            })
            .FirstOrDefault(item => item.Parameter.Ordinal == ordinal)?.Argument.Expression;
    }
}
