using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string QueueStackInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments)
    {
        var stack = method.ContainingType.OriginalDefinition.ToDisplayString()
            == "System.Collections.Generic.Stack<T>";
        return (name, arguments.Length) switch
        {
            ("Enqueue", 1) when !stack => $"{receiver}.push({arguments[0]})",
            ("Push", 1) when stack => $"{receiver}.unshift({arguments[0]})",
            ("Dequeue", 0) when !stack => QueueStackTake(receiver, remove: true),
            ("Pop", 0) when stack => QueueStackTake(receiver, remove: true),
            ("Peek", 0) => QueueStackTake(receiver, remove: false),
            ("Clear", 0) => $"{receiver}.length = 0",
            ("Contains", 1) when SupportsLinqEquality(method.ContainingType.TypeArguments[0]) =>
                $"{receiver}.includes({arguments[0]})",
            ("ToArray", 0) => $"{receiver}.slice()",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private string QueueStackTake(string receiver, bool remove)
    {
        _helpers.Require(JavaScriptHelper.QueueStack);
        return $"{_helpers.Name("queueStackTake")}({receiver}, {remove.ToString().ToLowerInvariant()})";
    }

}
