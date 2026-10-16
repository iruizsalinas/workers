using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private bool TryEmitRecordInvocation(InvocationExpressionSyntax invocation, IMethodSymbol? method,
        string receiver, string[] arguments, bool baseCall, out string result)
    {
        result = "";
        if (!IsSynthesizedRecordMember(method)) return false;
        var type = method!.ContainingType;
        if (IsSynthesizedRecordToString(method))
        {
            if (baseCall) _recordPrintDispatchTypes.Add((INamedTypeSymbol)ResolveType(type));
            result = baseCall ? $"{RecordTextFunction(type, invocation)}({receiver})" : RecordText(type, receiver, invocation);
            return true;
        }
        if (method.Name != "Equals" || method.Parameters is not [{ } parameter] || arguments.Length != 1)
            throw UnsupportedSymbol(method, invocation);
        var typed = SymbolEqualityComparer.Default.Equals(parameter.Type, type);
        if (!typed && !(RequiresUserClass(type) && (parameter.Type.SpecialType == SpecialType.System_Object
                || parameter.Type is INamedTypeSymbol { IsRecord: true })))
            throw UnsupportedSymbol(method, invocation);
        var other = typed ? "$other" : $"({TypePatternTest(type, "$other", null, invocation)} ? $other : null)";
        var comparison = baseCall && typed ? $"{RecordEqualityFunction(type, invocation)}($receiver, {other})"
            : RecordEquals(type, "$receiver", other, invocation);
        result = $"(($receiver, $other) => {{ if ($receiver == null) throw new TypeError(\"Object reference not set to an instance of an object.\"); " +
            $"return {comparison}; }})({receiver}, {arguments[0]})";
        return true;
    }
}
