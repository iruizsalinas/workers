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
            "System.Text.StringBuilder" => StringBuilderInvocation(invocation, method!, receiver, name, arguments),
            "System.Guid" => GuidInvocation(invocation, method!, receiver, name, arguments),
            "System.Text.Json.JsonElement" => JsonElementInvocation(invocation, method!, receiver, name, arguments),
            "System.Threading.CancellationToken" =>
                CancellationTokenInvocation(invocation, method!, receiver, name, arguments),
            "System.Threading.CancellationTokenSource" =>
                CancellationTokenSourceInvocation(invocation, method!, receiver, name, arguments),
            "System.Uri" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
            "Workers.Url" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
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
            _ when name == "ToString" && type?.SpecialType is SpecialType.System_Int32
                       or SpecialType.System_UInt32 or SpecialType.System_Single or SpecialType.System_Double =>
                NumericToString(invocation, method!, receiver, arguments),
            _ when name == "ToString" && arguments.Length == 0 && type?.SpecialType == SpecialType.System_Boolean =>
                $"({receiver} ? \"True\" : \"False\")",
            _ => ""
        };
        return result.Length != 0;
    }

    private string StringBuilderInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments)
    {
        _helpers.Require(JavaScriptHelper.StringBuilder);
        return (name, arguments.Length) switch
        {
            ("Append", 1) when method.Parameters[0].Type.SpecialType is
                SpecialType.System_String or SpecialType.System_Char =>
                $"{_helpers.Name("stringBuilderAppend")}({receiver}, {arguments[0]}, false)",
            ("AppendLine", 0) => $"{_helpers.Name("stringBuilderAppend")}({receiver}, null, true)",
            ("AppendLine", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"{_helpers.Name("stringBuilderAppend")}({receiver}, {arguments[0]}, true)",
            ("Append", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean =>
                $"{_helpers.Name("stringBuilderAppendValue")}({receiver}, {arguments[0]}, 1)",
            ("Append", 1) when method.Parameters[0].Type.SpecialType is SpecialType.System_Int32
                or SpecialType.System_UInt32 or SpecialType.System_Single or SpecialType.System_Double =>
                $"{_helpers.Name("stringBuilderAppendValue")}({receiver}, {arguments[0]}, 0)",
            ("Append", 2) when method.Parameters[0].Type.SpecialType == SpecialType.System_Char
                                && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 =>
                $"{_helpers.Name("stringBuilderAppendRepeat")}({receiver}, {arguments[0]}, {arguments[1]})",
            ("Insert", 2) when method.Parameters[0].Type.SpecialType == SpecialType.System_Int32
                                && method.Parameters[1].Type.SpecialType == SpecialType.System_String =>
                $"{_helpers.Name("stringBuilderInsert")}({receiver}, {arguments[0]}, {arguments[1]})",
            ("Remove", 2) when method.Parameters.All(parameter =>
                parameter.Type.SpecialType == SpecialType.System_Int32) =>
                $"{_helpers.Name("stringBuilderRemove")}({receiver}, {arguments[0]}, {arguments[1]})",
            ("Replace", 2) when method.Parameters.All(parameter => parameter.Type.SpecialType
                is SpecialType.System_String or SpecialType.System_Char) =>
                $"{_helpers.Name("stringBuilderReplace")}({receiver}, {arguments[0]}, {arguments[1]})",
            ("AppendJoin", _) when StringBuilderAppendJoinValues(source, method, arguments) is { } values =>
                $"{_helpers.Name("stringBuilderAppendJoin")}({receiver}, {arguments[0]}, {values})",
            ("Clear", 0) => $"{_helpers.Name("stringBuilderClear")}({receiver})",
            ("ToString", 0) => $"{_helpers.Name("stringBuilderText")}({receiver})",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private string? StringBuilderAppendJoinValues(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || method.Parameters.Length != 2
            || method.Parameters[0].Type.SpecialType is not (SpecialType.System_String or SpecialType.System_Char))
            return null;
        var valuesParameter = method.Parameters[1];
        var supportsStrings = valuesParameter.Type is IArrayTypeSymbol
            {
                ElementType.SpecialType: SpecialType.System_String
            } || valuesParameter.Type is INamedTypeSymbol
            {
                OriginalDefinition: { } definition,
                TypeArguments: [{ SpecialType: SpecialType.System_String }]
            } && definition.ToDisplayString() == "System.ReadOnlySpan<T>"
              || method.TypeArguments is [{ SpecialType: SpecialType.System_String }];
        if (!supportsStrings) return null;
        if (!valuesParameter.IsParams || arguments.Count == 2
            && IsStringCollection(_model.GetTypeInfo(source.ArgumentList.Arguments[1].Expression).Type))
            return arguments[1];
        return $"[{string.Join(", ", arguments.Skip(1))}]";
    }

    private static bool IsStringCollection(ITypeSymbol? type) => type is IArrayTypeSymbol
    {
        ElementType.SpecialType: SpecialType.System_String
    } || type is INamedTypeSymbol named
        && named.TypeArguments is [{ SpecialType: SpecialType.System_String }]
        && (named.OriginalDefinition.ToDisplayString() == "System.ReadOnlySpan<T>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString()
                == "System.Collections.Generic.IEnumerable<T>"));

    private string GuidInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ToString", 0) => receiver,
            ("ToString", 1) when SupportedGuidFormat(source, method) =>
                HelperInvocation(JavaScriptHelper.GuidFormat, [receiver, arguments[0]]),
            ("Equals", 1) when method.Parameters[0].Type.ToDisplayString() == "System.Guid" =>
                $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string JsonElementInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ToString", 0) => HelperInvocation(JavaScriptHelper.JsonElementToString, [receiver]),
            ("GetString", 0) => JsonElementValue(receiver, 0),
            ("GetBoolean", 0) => JsonElementValue(receiver, 1),
            ("GetInt32", 0) => JsonElementValue(receiver, 2),
            ("GetDouble", 0) => JsonElementValue(receiver, 3),
            ("GetArrayLength", 0) => JsonElementValue(receiver, 4),
            ("EnumerateArray", 0) => JsonElementValue(receiver, 5),
            ("GetProperty", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.JsonElementGetProperty, [receiver, arguments[0]]),
            ("Clone", 0) => receiver,
            _ => throw UnsupportedSymbol(method, source)
        };

    private string JsonElementValue(string receiver, int operation) =>
        HelperInvocation(JavaScriptHelper.JsonElementGetValue, [receiver, operation.ToString()]);

    private string CancellationTokenInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ThrowIfCancellationRequested", 0) =>
                HelperInvocation(JavaScriptHelper.CancellationCheck, [receiver]),
            ("Equals", 1) when method.Parameters[0].Type.ToDisplayString()
                                  == "System.Threading.CancellationToken" => $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string CancellationTokenSourceInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("Cancel", 0) => $"{receiver}.abort()",
            ("CancelAfter", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.CancellationCancelAfter, [receiver, arguments[0]]),
            _ => throw UnsupportedSymbol(method, source)
        };

    private static bool SupportedGuidFormat(InvocationExpressionSyntax source, IMethodSymbol method)
    {
        var format = InvocationArgument(source, method, "format");
        return format is LiteralExpressionSyntax literal
            && (literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression)
                || literal.Token.ValueText is "" or "D" or "d" or "N" or "n" or "B" or "b" or "P" or "p");
    }

    private string NumericToString(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string[] arguments)
    {
        var formatExpression = InvocationArgument(source, method, "format");
        var providerExpression = InvocationArgument(source, method, "provider");
        var providerSymbol = providerExpression is null ? null : _model.GetSymbolInfo(providerExpression).Symbol;
        if (arguments.Length != 2
            || formatExpression is not LiteralExpressionSyntax formatLiteral
            || providerSymbol is not IPropertySymbol
            {
                IsStatic: true,
                Name: "InvariantCulture",
                ContainingType: { } providerType
            }
            || providerType.ToDisplayString() != "System.Globalization.CultureInfo")
            throw UnsupportedSymbol(method, source);
        var format = formatLiteral.Token.ValueText;
        var type = method.ContainingType.SpecialType;
        var integral = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var code = format.Length == 0 ? '\0' : format[0];
        if (integral ? code is not ('D' or 'd' or 'X' or 'x') : code is not ('F' or 'f'))
            throw UnsupportedSymbol(method, source);
        var digits = format.Length == 1 ? (integral ? 0 : 2)
            : int.TryParse(format[1..], out var precision) ? precision : -1;
        if (digits is < 0 or > 100) throw UnsupportedSymbol(method, source);
        var kind = type switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 1,
            SpecialType.System_Single => 2,
            _ => 3
        };
        return HelperInvocation(JavaScriptHelper.NumericFormat,
            [receiver, arguments[0], kind.ToString(), digits.ToString()]);
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
            ("Trim", 0) => HelperInvocation(JavaScriptHelper.StringTrim, [receiver]),
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
                StringOrdinal(receiver, arguments[0], ignoreCase: false, operation: 6),
            ("Equals", 2) when StringComparisonMode(source, method) is { } equalsIgnoreCase =>
                StringOrdinal(receiver, arguments[0], equalsIgnoreCase, operation: 6),
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
