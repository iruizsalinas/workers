using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private bool TryEmitFrameworkInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments,
        out string result)
    {
        var type = method?.ContainingType;
        var typeName = type?.ToDisplayString();
        result = typeName switch
        {
            "System.Random" => RandomInvocation(invocation, method, name, arguments),
            "string" => StringInvocation(invocation, method, receiver, name, arguments),
            "System.DateTimeOffset" => DateTimeInvocation(invocation, method, receiver, name, arguments),
            "System.DateTime" => DateTimeInvocation(invocation, method, receiver, name, arguments),
            "System.TimeSpan" => TimeSpanInvocation(invocation, method, receiver, name, arguments),
            "System.Guid" when name == "ToString" && arguments.Length == 0 => receiver,
            "System.Uri" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
            "Workers.Url" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
            "System.Text.Json.JsonElement" when name == "ToString" && arguments.Length == 0 =>
                $"{_helpers.Require(JavaScriptHelper.JsonElementToString)}({receiver})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
                   && name == "Add" && arguments.Length == 1 => $"{receiver}.push({arguments[0]})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
                   && name == "Contains" && arguments.Length == 1
                   && SupportsLinqEquality(type.TypeArguments[0]) =>
                HelperInvocation(JavaScriptHelper.LinqContains, [receiver, arguments[0]]),
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
                   && SupportsLinqEquality(type.TypeArguments[0])
                   && name == "Add" && arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.SetAdd)}({receiver}, {arguments[0]})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
                   && SupportsLinqEquality(type.TypeArguments[0])
                   && name == "Contains" && arguments.Length == 1 => $"{receiver}.has({arguments[0]})",
            _ when name == "ToString" && arguments.Length == 0
                   && type?.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Decimal =>
                $"String({receiver})",
            _ when name == "ToString" && arguments.Length == 0 && type?.SpecialType == SpecialType.System_Boolean =>
                $"({receiver} ? \"True\" : \"False\")",
            _ => ""
        };
        return result.Length != 0;
    }

    private string RandomInvocation(
        SyntaxNode source,
        IMethodSymbol? method,
        string name,
        string[] arguments) => name switch
        {
            "NextDouble" when arguments.Length == 0 => "Math.random()",
            "Next" when arguments.Length == 0 => "Math.floor(Math.random() * 2147483647)",
            "Next" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.RandomNext)}(0, {arguments[0]})",
            "Next" when arguments.Length == 2 =>
                $"{_helpers.Require(JavaScriptHelper.RandomNext)}({arguments[0]}, {arguments[1]})",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string StringInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("Trim", 0) => $"{receiver}.trim()",
            ("ToLowerInvariant", 0) => $"{receiver}.toLowerCase()",
            ("ToUpperInvariant", 0) => $"{receiver}.toUpperCase()",
            ("Contains", 1) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.StringContains, [receiver, arguments[0]]),
            ("Contains", 1) => $"{receiver}.includes({arguments[0]})",
            ("StartsWith", 1) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.StringStartsWith, [receiver, arguments[0]]),
            ("StartsWith", 1) => $"{receiver}.startsWith({arguments[0]})",
            ("EndsWith", 1) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.StringEndsWith, [receiver, arguments[0]]),
            ("EndsWith", 1) => $"{receiver}.endsWith({arguments[0]})",
            ("Substring", 1) => HelperInvocation(JavaScriptHelper.StringSubstring, [receiver, arguments[0], "null"]),
            ("Substring", 2) => HelperInvocation(JavaScriptHelper.StringSubstring, [receiver, .. arguments]),
            ("Replace", 2) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.StringReplace, [receiver, .. arguments]),
            ("Replace", 2) => $"{receiver}.replaceAll({arguments[0]}, {arguments[1]})",
            ("Replace", 3) when StringComparisonMode(source, method) == false =>
                HelperInvocation(JavaScriptHelper.StringReplace, [receiver, arguments[0], arguments[1]]),
            ("Equals", 1) when method?.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                StringOrdinal(receiver, arguments[0], ignoreCase: false, operation: 0),
            ("Equals", 2) when StringComparisonMode(source, method) is { } equalsIgnoreCase =>
                StringOrdinal(receiver, arguments[0], equalsIgnoreCase, operation: 0),
            ("Contains", 2) when StringComparisonMode(source, method) is { } containsIgnoreCase =>
                StringOrdinal(receiver, arguments[0], containsIgnoreCase, operation: 1),
            ("StartsWith", 2) when StringComparisonMode(source, method) is { } startsIgnoreCase =>
                StringOrdinal(receiver, arguments[0], startsIgnoreCase, operation: 2),
            ("EndsWith", 2) when StringComparisonMode(source, method) is { } endsIgnoreCase =>
                StringOrdinal(receiver, arguments[0], endsIgnoreCase, operation: 3),
            ("IndexOf", _) => StringIndexOf(source, method, receiver, arguments),
            ("LastIndexOf", 1) => StringOrdinal(receiver, arguments[0], ignoreCase: false, operation: 5),
            ("LastIndexOf", 2) when StringComparisonMode(source, method) is { } lastIgnoreCase =>
                StringOrdinal(receiver, arguments[0], lastIgnoreCase, operation: 5),
            ("Remove", 1) => HelperInvocation(JavaScriptHelper.StringRemove, [receiver, arguments[0], "null"]),
            ("Remove", 2) => HelperInvocation(JavaScriptHelper.StringRemove, [receiver, .. arguments]),
            ("Insert", 2) => HelperInvocation(JavaScriptHelper.StringInsert, [receiver, .. arguments]),
            ("PadLeft", 1) => HelperInvocation(JavaScriptHelper.StringPad, [receiver, arguments[0], "\" \"", "true"]),
            ("PadLeft", 2) => HelperInvocation(JavaScriptHelper.StringPad, [receiver, .. arguments, "true"]),
            ("PadRight", 1) => HelperInvocation(JavaScriptHelper.StringPad, [receiver, arguments[0], "\" \"", "false"]),
            ("PadRight", 2) => HelperInvocation(JavaScriptHelper.StringPad, [receiver, .. arguments, "false"]),
            ("ToCharArray", 0) => HelperInvocation(JavaScriptHelper.StringToCharArray, [receiver, "null", "null"]),
            ("ToCharArray", 2) => HelperInvocation(JavaScriptHelper.StringToCharArray, [receiver, .. arguments]),
            ("Split", 2) when method?.Parameters[0].Type.SpecialType == SpecialType.System_Char =>
                HelperInvocation(JavaScriptHelper.StringSplit, [receiver, .. arguments]),
            _ => throw UnsupportedSymbol(method, source)
        };

    private string StringStaticInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string name,
        string[] arguments) => name switch
        {
            "IsNullOrEmpty" when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.StringIsNullOrEmpty, arguments),
            "IsNullOrWhiteSpace" when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.StringIsNullOrWhiteSpace, arguments),
            "Join" => StringJoin(source, method, arguments),
            "Concat" => StringConcat(source, method, arguments),
            "Equals" when arguments.Length == 2 =>
                StringOrdinal(arguments[0], arguments[1], ignoreCase: false, operation: 0),
            "Equals" when arguments.Length == 3 && StringComparisonMode(source, method) is { } ignoreCase =>
                StringOrdinal(arguments[0], arguments[1], ignoreCase, operation: 0),
            _ => throw UnsupportedSymbol(method, source)
        };

    private string StringJoin(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        if (method.Parameters.Length != 2 || method.TypeArguments.Length != 0
            || method.Parameters[0].Type.SpecialType is not (SpecialType.System_String or SpecialType.System_Char))
            throw UnsupportedSymbol(method, source);
        var values = StringSequenceArgument(method.Parameters[1].Type, method.Parameters[1].IsParams, arguments[1..]);
        if (values is null) throw UnsupportedSymbol(method, source);
        return HelperInvocation(JavaScriptHelper.StringJoin, [arguments[0], values]);
    }

    private string StringConcat(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        if (method.TypeArguments.Length != 0)
            throw UnsupportedSymbol(method, source);
        if (method.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String))
            return HelperInvocation(JavaScriptHelper.StringJoin, ["\"\"", $"[{string.Join(", ", arguments)}]"]);
        if (method.Parameters is [{ Type: { } type, IsParams: var isParams }])
        {
            var values = StringSequenceArgument(type, isParams, arguments);
            if (values is not null)
                return HelperInvocation(JavaScriptHelper.StringJoin, ["\"\"", values]);
        }
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

    private string StringIndexOf(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string[] arguments)
    {
        if (method is null || arguments.Length == 0 || method.Parameters[0].Type.SpecialType
            is not (SpecialType.System_String or SpecialType.System_Char))
            throw UnsupportedSymbol(method, source);
        var ignoreCase = StringComparisonMode(source, method) ?? false;
        if (method.Parameters.Any(parameter => parameter.Type.ToDisplayString() == "System.StringComparison")
            && StringComparisonMode(source, method) is null)
            throw UnsupportedSymbol(method, source);
        var start = ParameterArgument(method, arguments, "startIndex") ?? "null";
        var count = ParameterArgument(method, arguments, "count") ?? "null";
        return StringOrdinal(receiver, arguments[0], ignoreCase, operation: 4, start, count);
    }

    private string StringOrdinal(
        string source,
        string value,
        bool ignoreCase,
        int operation,
        string start = "null",
        string count = "null") =>
        HelperInvocation(JavaScriptHelper.StringOrdinal,
            [source, value, ignoreCase ? "true" : "false", operation.ToString(), start, count]);

    private static string? ParameterArgument(
        IMethodSymbol method,
        IReadOnlyList<string> arguments,
        string name)
    {
        var ordinal = method.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Ordinal;
        return ordinal is not null && ordinal < arguments.Count ? arguments[ordinal.Value] : null;
    }

    private static bool? StringComparisonMode(InvocationExpressionSyntax source, IMethodSymbol? method) =>
        InvocationArgument(source, method, "comparisonType") is { } argument
        && argument is MemberAccessExpressionSyntax comparison
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

    private string DateTimeInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => name switch
        {
            "ToString" when arguments.Length == 1
                            && source.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax format
                            && format.Token.ValueText is "O" or "o" =>
                method?.ContainingType.ToDisplayString() == "System.DateTime"
                    ? DateTimeRoundTrip(receiver, includeOffset: false)
                    : DateTimeRoundTrip(receiver),
            "AddMonths" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeAddMonths)}({receiver}, {arguments[0]})",
            "AddYears" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeAddMonths)}({receiver}, ({arguments[0]}) * 12)",
            "AddDays" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 86400000"),
            "AddHours" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 3600000"),
            "AddMinutes" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 60000"),
            "AddSeconds" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 1000"),
            "AddMilliseconds" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, arguments[0]),
            "Add" when HasTimeSpanParameter(method) => DateTimeAddMilliseconds(receiver, arguments[0]),
            "Subtract" when HasTimeSpanParameter(method) => DateTimeAddMilliseconds(receiver, $"-({arguments[0]})"),
            "Subtract" when arguments.Length == 1 && HasSameDateParameter(method) =>
                $"new Date({receiver}).getTime() - new Date({arguments[0]}).getTime()",
            "CompareTo" when arguments.Length == 1 && HasSameDateParameter(method) =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, [receiver, arguments[0]]),
            "Equals" when arguments.Length == 1 && HasSameDateParameter(method) =>
                $"{HelperInvocation(JavaScriptHelper.DateTimeCompare, [receiver, arguments[0]])} === 0",
            "ToUniversalTime" when arguments.Length == 0
                                   && method?.ContainingType.ToDisplayString() == "System.DateTimeOffset" =>
                $"new Date({receiver})",
            "ToUnixTimeMilliseconds" when arguments.Length == 0 => $"new Date({receiver}).getTime()",
            "ToUnixTimeSeconds" when arguments.Length == 0 => $"Math.floor(new Date({receiver}).getTime() / 1000)",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string TimeSpanInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => name switch
        {
            "Add" when HasTimeSpanParameter(method) => TimeSpanArithmetic(receiver, arguments[0], "+"),
            "Subtract" when HasTimeSpanParameter(method) => TimeSpanArithmetic(receiver, arguments[0], "-"),
            "Negate" when arguments.Length == 0 => TimeSpanNegate(receiver),
            "Duration" when arguments.Length == 0 => TimeSpanDuration(receiver),
            "CompareTo" when HasTimeSpanParameter(method) => TimeSpanCompare(receiver, arguments[0]),
            "Equals" when HasTimeSpanParameter(method) => $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string TimeSpanArithmetic(string left, string right, string operation) =>
        $"{_helpers.Require(JavaScriptHelper.TimeSpan)}(({left}) {operation} ({right}))";

    private string TimeSpanNegate(string value)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanNegate")}({value})";
    }

    private string TimeSpanDuration(string value)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanDuration")}({value})";
    }

    private static bool HasSameDateParameter(IMethodSymbol? method) => method is { Parameters.Length: 1 }
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, method.Parameters[0].Type);

    private static bool HasTimeSpanParameter(IMethodSymbol? method) => method is { Parameters: [{ Type: { } type }] }
        && type.ToDisplayString() == "System.TimeSpan";

    private string DateTimeAddMilliseconds(string receiver, string delta) =>
        $"{_helpers.Require(JavaScriptHelper.DateTimeAddMilliseconds)}({receiver}, {delta})";
}
