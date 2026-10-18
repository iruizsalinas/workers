using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static ISymbol RecordPositionalMember(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current.GetMembers(name).FirstOrDefault(member => !member.IsStatic && member is IPropertySymbol or IFieldSymbol) is { } member)
                return member;
        throw new InvalidOperationException($"Record positional member '{type}.{name}' was not found.");
    }

    private bool TryEmitRecordInvocation(InvocationExpressionSyntax invocation, IMethodSymbol? method,
        string receiver, string[] arguments, bool baseCall, out string result)
    {
        result = "";
        if (!IsSynthesizedRecordMember(method)) return false;
        var type = method!.ContainingType;
        if (method.Name == "Deconstruct" && method.Parameters.All(parameter => parameter.RefKind == RefKind.Out))
        {
            var outputs = method.Parameters.Select((parameter, index) =>
            {
                var member = RecordPositionalMember(type, parameter.Name);
                return $"$out{index}.value = {CopyValue(parameter.Type, UserMemberAccess("$receiver", member))};";
            });
            var parameters = method.Parameters.Select((_, index) => $"$out{index}");
            result = $"(($receiver, {string.Join(", ", parameters)}) => {{ {string.Join(" ", outputs)} }})({receiver}, {string.Join(", ", arguments)})";
            return true;
        }
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
        result = $"(($receiver, $other) => {{ if ($receiver == null) throw {NullReferenceException()}; " +
            $"return {comparison}; }})({receiver}, {arguments[0]})";
        return true;
    }
}
