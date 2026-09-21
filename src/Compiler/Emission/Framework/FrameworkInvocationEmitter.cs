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
            _ when type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.Queue<T>" or
                       "System.Collections.Generic.Stack<T>" =>
                QueueStackInvocation(invocation, method!, receiver, name, arguments),
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

}
