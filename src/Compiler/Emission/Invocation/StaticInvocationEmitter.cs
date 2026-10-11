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
            ("System.Collections.Generic.KeyValuePair", "Create") when arguments.Length == 2 => $"[{arguments[0]}, {arguments[1]}]",
            ("Workers.Performance", "Now") => "performance.now()",
            ("int", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 0),
            ("uint", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 1),
            ("double", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 2),
            ("bool", "Parse") when HasParameters(method, SpecialType.System_String) => NumericParse(arguments[0], 3),
            ("string", _) when method?.IsStatic == true => StringStaticInvocation(invocation, method, name!, arguments),
            ("Workers.Timers", "SetTimeout") => $"setTimeout({arguments[0]}, {arguments[1]})",
            ("Workers.Timers", "ClearTimeout") => $"clearTimeout({arguments[0]})",
            ("Workers.Response", "Json") => JsonResponse(arguments),
            ("Workers.Response", "Redirect") => HelperInvocation(JavaScriptHelper.Redirect, arguments),
            _ => ""
        };
        return result.Length != 0;
    }

    private static string JsonResponse(string[] arguments)
    {
        var settings = new[] { "status", "headers", "statusText" }
            .Take(arguments.Length - 1)
            .Select((name, index) => $"{name}: {arguments[index + 1]}{(index > 0 ? " ?? undefined" : "")}");
        return arguments.Length == 1 ? $"Response.json({arguments[0]})"
            : $"Response.json({arguments[0]}, {{ {string.Join(", ", settings)} }})";
    }

    private static bool HasParameters(IMethodSymbol? method, params SpecialType[] types) =>
        method is not null && method.Parameters.Select(parameter => parameter.Type.SpecialType).SequenceEqual(types);

    private string HelperInvocation(JavaScriptHelper helper, IReadOnlyList<string> arguments) =>
        $"{_helpers.Require(helper)}({string.Join(", ", arguments)})";

    private string NumericParse(string value, int kind) =>
        $"{_helpers.Require(JavaScriptHelper.NumericParse)}({value}, {kind})";

    private string TaskWhenAll(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IReadOnlyList<string> arguments)
    {
        var helper = _helpers.Require(JavaScriptHelper.TaskWhenAll);
        if (!method.Parameters[0].IsParams
            || arguments.Count == 1 && IsTaskCollection(_model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type))
            return $"{helper}({arguments[0]})";
        return $"{helper}([{string.Join(", ", arguments)}])";
    }

    private static bool IsTaskCollection(ITypeSymbol? type) => type is IArrayTypeSymbol
        || type is INamedTypeSymbol named && named.AllInterfaces.Any(item =>
            item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

}
