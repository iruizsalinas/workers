using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static string DefaultFieldValue(ITypeSymbol type, SyntaxNode source)
    {
        if (type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return "null";
        if (type.SpecialType == SpecialType.System_Boolean) return "false";
        if (type.SpecialType == SpecialType.System_Char) return "\"\\u0000\"";
        if (type.TypeKind == TypeKind.Enum || type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Decimal)
            return "0";
        throw Unsupported("WRK108", source);
    }

    private void EmitHandler(string eventName, MethodDeclarationSyntax method)
    {
        _model = _compilation.GetSemanticModel(method.SyntaxTree);
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterName));
        var isAsync = method.Modifiers.Any(token => token.RawKind == (int)SyntaxKind.AsyncKeyword);
        _output.Append(isAsync ? "async " : "").Append("function ").Append(EventName(eventName)).Append('(').Append(parameters).AppendLine(") {");
        if (method.ExpressionBody is not null)
            _output.Append("  return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 1);
        _output.AppendLine("}").AppendLine();
    }

    private string QueueUserMethod(IMethodSymbol method, SyntaxNode callSite)
    {
        method = method.OriginalDefinition;
        if (!method.IsStatic || method.IsGenericMethod || method.IsExtensionMethod || method.ContainingType.IsGenericType
            || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams)
            || method.DeclaringSyntaxReferences.Length != 1
            || method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax)
            throw UnsupportedSymbol(method, callSite);
        if (_userMethods.TryGetValue(method, out var existing))
            return existing;
        var name = _names.ForMethod(method, _userMethods.Count);
        _userMethods.Add(method, name);
        _pendingUserMethods.Enqueue(method);
        return name;
    }

    private string EmitUserInvocation(IMethodSymbol method, InvocationExpressionSyntax invocation, IReadOnlyList<string> arguments)
    {
        return $"{QueueUserMethod(method, invocation)}({string.Join(", ", arguments)})";
    }

    private void EmitUserMethods()
    {
        while (_pendingUserMethods.Count != 0)
        {
            var symbol = _pendingUserMethods.Dequeue();
            if (!_emittedUserMethods.Add(symbol)) continue;
            var declaration = (MethodDeclarationSyntax)symbol.DeclaringSyntaxReferences.Single().GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var parameters = string.Join(", ", declaration.ParameterList.Parameters.Select(ParameterDeclaration));
            var isAsync = declaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(declaration);
            _output.Append(isAsync ? "async " : "").Append(isIterator ? "function* " : "function ")
                .Append(_userMethods[symbol]).Append('(').Append(parameters).AppendLine(") {");
            if (declaration.ExpressionBody is not null)
                _output.Append("  return ").Append(Expression(declaration.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in declaration.Body?.Statements ?? []) EmitStatement(statement, 1);
            _output.AppendLine("}").AppendLine();
        }
    }

}
