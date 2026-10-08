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
        if (type.TypeKind == TypeKind.Enum || type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return "0";
        if (type.ToDisplayString() == "System.Guid") return "\"00000000-0000-0000-0000-000000000000\"";
        if (type.ToDisplayString() is "System.DateTimeOffset" or "System.DateTime") return "new Date(-62135596800000)";
        if (type.ToDisplayString() == "System.TimeSpan") return "0";
        // default(JsonElement) has ValueKind Undefined, which the JsonElement helpers report for undefined.
        if (type.ToDisplayString() == "System.Text.Json.JsonElement") return "undefined";
        throw Unsupported("WRK108", source);
    }

    private void EmitHandler(string eventName, MethodDeclarationSyntax method)
    {
        _model = _compilation.GetSemanticModel(method.SyntaxTree);
        _diagnosticNode = method;
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterName));
        var isAsync = method.Modifiers.Any(token => token.RawKind == (int)SyntaxKind.AsyncKeyword);
        _output.Append(isAsync ? "async " : "").Append("function ").Append(EventName(eventName)).Append('(').Append(parameters).AppendLine(") {");
        if (method.ExpressionBody is not null)
            _output.Append("  return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
        else
            EmitStatements(method.Body?.Statements ?? [], 1);
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
            _diagnosticNode = declaration;
            var parameters = string.Join(", ", declaration.ParameterList.Parameters.Select(ParameterDeclaration));
            var isAsync = declaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(declaration);
            _output.Append(isAsync ? "async " : "").Append(isIterator ? "function* " : "function ")
                .Append(_userMethods[symbol]).Append('(').Append(parameters).AppendLine(") {");
            if (declaration.ExpressionBody is not null)
                _output.Append("  return ").Append(Expression(declaration.ExpressionBody.Expression)).AppendLine(";");
            else
                EmitStatements(declaration.Body?.Statements ?? [], 1);
            _output.AppendLine("}").AppendLine();
        }
    }

}
