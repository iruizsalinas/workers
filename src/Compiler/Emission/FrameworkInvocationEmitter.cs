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
            "System.Guid" when name == "ToString" && arguments.Length == 0 => receiver,
            "System.Uri" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
            "Workers.Url" when name == "ToString" && arguments.Length == 0 => $"{receiver}.toString()",
            "System.Text.Json.JsonElement" when name == "ToString" && arguments.Length == 0 =>
                $"JSON.stringify({receiver})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
                   && name == "Add" && arguments.Length == 1 => $"{receiver}.push({arguments[0]})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
                   && name == "Add" && arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.SetAdd)}({receiver}, {arguments[0]})",
            _ when type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
                   && name == "Contains" && arguments.Length == 1 => $"{receiver}.has({arguments[0]})",
            _ when name == "ToString" && arguments.Length == 0
                   && type?.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Decimal =>
                $"String({receiver})",
            _ when name == "ToString" && arguments.Length == 0 && type?.SpecialType == SpecialType.System_Boolean =>
                $"String({receiver})",
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
            ("Contains", 1) => $"{receiver}.includes({arguments[0]})",
            ("StartsWith", 1) => $"{receiver}.startsWith({arguments[0]})",
            ("EndsWith", 1) => $"{receiver}.endsWith({arguments[0]})",
            ("Substring", 1) => $"{receiver}.slice({arguments[0]})",
            ("Substring", 2) => $"{receiver}.slice({arguments[0]}, {arguments[0]} + {arguments[1]})",
            ("Replace", 2) => $"{receiver}.replaceAll({arguments[0]}, {arguments[1]})",
            ("IndexOf", 2) when source.ArgumentList.Arguments[1].Expression is MemberAccessExpressionSyntax comparison
                                && comparison.Name.Identifier.Text == "Ordinal" =>
                $"{receiver}.indexOf({arguments[0]})",
            ("Split", 2) when source.ArgumentList.Arguments[1].Expression is MemberAccessExpressionSyntax option
                              && option.Name.Identifier.Text == "RemoveEmptyEntries" =>
                $"{receiver}.split({arguments[0]}).filter(Boolean)",
            _ => throw UnsupportedSymbol(method, source)
        };

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
                $"new Date({receiver}).toISOString().replace(/(\\.\\d{{3}})Z$/, \"$1\" + \"0000+00:00\")",
            "AddDays" when arguments.Length == 1 =>
                $"new Date(new Date({receiver}).getTime() + ({arguments[0]}) * 86400000)",
            "AddSeconds" when arguments.Length == 1 =>
                $"new Date(new Date({receiver}).getTime() + ({arguments[0]}) * 1000)",
            "ToUnixTimeMilliseconds" when arguments.Length == 0 => $"new Date({receiver}).getTime()",
            _ => throw UnsupportedSymbol(method, source)
        };
}
