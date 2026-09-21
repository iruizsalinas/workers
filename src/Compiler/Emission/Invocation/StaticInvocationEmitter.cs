using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private bool TryEmitStaticInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string? type,
        string? name,
        string[] arguments,
        string? receiverOverride,
        out string result)
    {
        result = (type, name) switch
        {
            ("System.TimeSpan", "FromDays") when arguments.Length == 1 => TimeSpanValue(arguments[0], 86400000),
            ("System.TimeSpan", "FromHours") when arguments.Length == 1 => TimeSpanValue(arguments[0], 3600000),
            ("System.TimeSpan", "FromMinutes") when arguments.Length == 1 => TimeSpanValue(arguments[0], 60000),
            ("System.TimeSpan", "FromSeconds") when arguments.Length == 1 => TimeSpanValue(arguments[0], 1000),
            ("System.TimeSpan", "FromMilliseconds") when arguments.Length == 1 => TimeSpanValue(arguments[0], 1),
            ("System.TimeSpan", "Compare") when arguments.Length == 2 => TimeSpanCompare(arguments[0], arguments[1]),
            ("System.TimeSpan", "Equals") when arguments.Length == 2 => $"{arguments[0]} === {arguments[1]}",
            ("System.DateTimeOffset", "FromUnixTimeMilliseconds") when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeFromUnixTime)}({arguments[0]}, false)",
            ("System.DateTimeOffset", "FromUnixTimeSeconds") when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeFromUnixTime)}({arguments[0]}, true)",
            ("System.DateTime", "IsLeapYear") when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.DateTimeIsLeapYear, arguments),
            ("System.DateTime", "DaysInMonth") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeDaysInMonth, arguments),
            ("System.DateTime", "Compare") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, arguments),
            ("System.DateTimeOffset", "Compare") when arguments.Length == 2 =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, arguments),
            ("System.Guid", "Parse") when HasParameters(method, SpecialType.System_String) =>
                HelperInvocation(JavaScriptHelper.GuidParse, arguments),
            ("System.Text.Json.JsonSerializer", "Serialize") when arguments.Length == 1 =>
                $"JSON.stringify({arguments[0]})",
            ("System.Text.Json.JsonSerializer", "SerializeToUtf8Bytes") when arguments.Length == 1 =>
                $"new TextEncoder().encode(JSON.stringify({arguments[0]}))",
            ("System.Text.Json.JsonSerializer", "Deserialize") when arguments.Length == 1 =>
                JsonDeserialize(invocation, method!, arguments[0]),
            ("System.Console", "WriteLine") when arguments.Length == 1 => $"console.log({arguments[0]})",
            ("System.Guid", "NewGuid") => "globalThis.crypto.randomUUID()",
            ("Workers.Performance", "Now") => "performance.now()",
            ("System.Uri", "UnescapeDataString") when HasParameters(method, SpecialType.System_String) => $"decodeURIComponent({arguments[0]})",
            ("System.Uri", "EscapeDataString") when HasParameters(method, SpecialType.System_String) => $"{_helpers.Require(JavaScriptHelper.EscapeDataString)}({arguments[0]})",
            ("System.Convert", "FromHexString") when HasParameters(method, SpecialType.System_String) => $"{_helpers.Require(JavaScriptHelper.HexDecode)}({arguments[0]})",
            ("System.Convert", "ToHexString") when arguments.Length == 1 && method?.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => $"Array.from({arguments[0]}, byte => byte.toString(16).padStart(2, \"0\")).join(\"\").toUpperCase()",
            ("System.Convert", "ToBase64String") when arguments.Length == 1 && method?.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => $"{_helpers.Require(JavaScriptHelper.Base64)}({arguments[0]})",
            ("System.Convert", "FromBase64String") when HasParameters(method, SpecialType.System_String) => Base64Decode(arguments[0]),
            ("System.Text.Encoding", "GetBytes") when HasParameters(method, SpecialType.System_String) && IsUtf8EncodingInvocation(invocation) => $"new TextEncoder().encode({arguments[0]})",
            ("int", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 0),
            ("uint", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 1),
            ("float", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 2),
            ("double", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 3),
            ("bool", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 4),
            ("System.Math" or "System.MathF", _) => MathInvocation(invocation, method!, name!, arguments),
            ("string", _) when method?.IsStatic == true => StringStaticInvocation(invocation, method, name!, arguments),
            ("System.Text.RegularExpressions.Regex", "IsMatch") when method?.IsStatic == true && arguments.Length == 2 =>
                RegexIsMatch(invocation, method, arguments),
            ("Workers.Timers", "SetTimeout") => $"setTimeout({arguments[0]}, {arguments[1]})",
            ("Workers.Timers", "ClearTimeout") => $"clearTimeout({arguments[0]})",
            ("Workers.Body", "Text" or "FromBytes" or "FromStream" or "FromFormData" or "FromQueryParameters") => arguments[0],
            ("Workers.Body", "Json") => $"JSON.stringify({arguments[0]})",
            ("Workers.Response", _) => ResponseInvocation(invocation, method!, name!, arguments, receiverOverride),
            _ => ""
        };
        return result.Length != 0;
    }

    private static bool HasParameters(IMethodSymbol? method, params SpecialType[] types) =>
        method is not null && method.Parameters.Select(parameter => parameter.Type.SpecialType).SequenceEqual(types);

    private string HelperInvocation(JavaScriptHelper helper, IReadOnlyList<string> arguments) =>
        $"{_helpers.Require(helper)}({string.Join(", ", arguments)})";

    private string TimeSpanValue(string value, int factor) =>
        $"{_helpers.Require(JavaScriptHelper.TimeSpan)}({(factor == 1 ? value : $"({value}) * {factor}")})";

    private string TimeSpanCompare(string left, string right)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanCompare")}({left}, {right})";
    }

    private bool IsUtf8EncodingInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax receiver }
        && _model.GetSymbolInfo(receiver).Symbol is IPropertySymbol
        {
            IsStatic: true,
            Name: "UTF8",
            ContainingType: { } containingType
        }
        && containingType.ToDisplayString() == "System.Text.Encoding";

    private string Base64Decode(string value)
    {
        _helpers.Require(JavaScriptHelper.Base64);
        return $"{_helpers.Name("base64Decode")}({value})";
    }

    private string NumericParse(string value, int kind) =>
        $"{_helpers.Require(JavaScriptHelper.NumericParse)}({value}, {kind})";

    private string JsonDeserialize(InvocationExpressionSyntax source, IMethodSymbol method, string value)
    {
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol resultType && IsUserInstanceType(resultType))
            RegisterJsonMaterializer(resultType, source);
        var input = method.Parameters[0].Type;
        var parsed = input.SpecialType == SpecialType.System_String
            ? $"JSON.parse({value})"
            : input.ToDisplayString() == "System.ReadOnlySpan<byte>" || input is IArrayTypeSymbol
            {
                ElementType.SpecialType: SpecialType.System_Byte
            }
                ? $"JSON.parse(new TextDecoder().decode({value}))"
                : throw UnsupportedSymbol(method, source);
        if (method.TypeArguments.FirstOrDefault() is INamedTypeSymbol target && IsUserInstanceType(target))
            return $"{QueueUserType(target, source)}.$fromJSON({parsed})";
        return parsed;
    }

    private string MathInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string name,
        string[] arguments)
    {
        var single = method.ContainingType.ToDisplayString() == "System.MathF";
        var type = method.ReturnType.SpecialType;
        if (type is not (SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Single or SpecialType.System_Double))
            throw UnsupportedSymbol(method, source);
        string result = name switch
        {
            "Abs" when arguments.Length == 1 && type == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.MathAbsInt, arguments),
            "Abs" when arguments.Length == 1 => $"Math.abs({arguments[0]})",
            "Clamp" when arguments.Length == 3 => HelperInvocation(JavaScriptHelper.MathClamp, arguments),
            "Round" when arguments.Length == 1 =>
                HelperInvocation(JavaScriptHelper.MathRound, [arguments[0], "0", single ? "6" : "15"]),
            "Round" when arguments.Length == 2
                              && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.MathRound, [.. arguments, single ? "6" : "15"]),
            "Floor" when arguments.Length == 1 => $"Math.floor({arguments[0]})",
            "Ceiling" when arguments.Length == 1 => $"Math.ceil({arguments[0]})",
            "Truncate" when arguments.Length == 1 => $"Math.trunc({arguments[0]})",
            "Sign" when arguments.Length == 1 => HelperInvocation(JavaScriptHelper.MathSign, arguments),
            "Min" when arguments.Length == 2 => $"Math.min({arguments[0]}, {arguments[1]})",
            "Max" when arguments.Length == 2 => $"Math.max({arguments[0]}, {arguments[1]})",
            "Pow" when arguments.Length == 2 => $"Math.pow({arguments[0]}, {arguments[1]})",
            "Sqrt" when arguments.Length == 1 => $"Math.sqrt({arguments[0]})",
            "Log" when arguments.Length == 1 => $"Math.log({arguments[0]})",
            "Log" when arguments.Length == 2 => HelperInvocation(JavaScriptHelper.MathLog, arguments),
            "Log10" when arguments.Length == 1 => $"Math.log10({arguments[0]})",
            "Exp" when arguments.Length == 1 => $"Math.exp({arguments[0]})",
            "Sin" when arguments.Length == 1 => $"Math.sin({arguments[0]})",
            "Cos" when arguments.Length == 1 => $"Math.cos({arguments[0]})",
            "Tan" when arguments.Length == 1 => $"Math.tan({arguments[0]})",
            "Asin" when arguments.Length == 1 => $"Math.asin({arguments[0]})",
            "Acos" when arguments.Length == 1 => $"Math.acos({arguments[0]})",
            "Atan" when arguments.Length == 1 => $"Math.atan({arguments[0]})",
            "Atan2" when arguments.Length == 2 => $"Math.atan2({arguments[0]}, {arguments[1]})",
            _ => throw UnsupportedSymbol(method, source)
        };
        return single && type == SpecialType.System_Single ? $"Math.fround({result})" : result;
    }

    private string RegexIsMatch(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        if (invocation.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax literal
            || !IsCompatibleRegexPattern(literal.Token.ValueText))
            throw new NotSupportedException(
                "WRK120: Regex.IsMatch supports only literal patterns that are compatible with JavaScript regular expressions.");
        return $"new RegExp({arguments[1]}).test({arguments[0]})";
    }

    private static bool IsCompatibleRegexPattern(string pattern) =>
        pattern.All(character => character <= 0x7f)
        && !pattern.Contains('\\')
        && !pattern.Contains("(?", StringComparison.Ordinal)
        && !pattern.Contains("-[", StringComparison.Ordinal);

    private string TaskWhenAll(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        if (!method.Parameters[0].IsParams)
            return $"Promise.all({arguments[0]})";
        if (arguments.Count == 1 && IsTaskCollection(_model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type))
            return $"Promise.all({arguments[0]})";
        return $"Promise.all([{string.Join(", ", arguments)}])";
    }

    private static bool IsTaskCollection(ITypeSymbol? type) => type is IArrayTypeSymbol
        || type is INamedTypeSymbol named && named.AllInterfaces.Any(item =>
            item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

}
