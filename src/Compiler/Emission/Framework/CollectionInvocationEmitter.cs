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

}
