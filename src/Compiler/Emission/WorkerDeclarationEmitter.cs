using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string EventName(string eventName) => _names.Get("event:" + eventName, eventName);

    private void EmitHtmlHandler(ClassDeclarationSyntax declaration)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var type = _model.GetDeclaredSymbol(declaration)!;
        _output.Append("class ").Append(UserIdentifier(type, declaration.Identifier)).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1)
            throw Unsupported("WRK109", constructors[1]);
        var constructor = constructors.SingleOrDefault();
        if (constructor is not null)
        {
            _output.Append("  constructor(")
                .Append(string.Join(", ", constructor.ParameterList.Parameters.Select(ParameterName)))
                .AppendLine(") {");
            foreach (var statement in constructor.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>().Where(item => item.Modifiers.Any(SyntaxKind.PublicKeyword)))
        {
            var name = LowerNativeMethodName(method.Identifier.Text);
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterName));
            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            _output.Append("  ").Append(isAsync ? "async " : "").Append(name).Append('(').Append(parameters).AppendLine(") {");
            if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

    private void EmitDurableObject(ClassDeclarationSyntax declaration)
    {
        var model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(declaration)!;
        var attribute = symbol.GetAttributes().Single(item => item.AttributeClass?.ToDisplayString() == "Workers.DurableObjectAttribute");
        var exportName = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value?.ToString() ?? symbol.Name
            : symbol.Name;
        if (!IsLegalJavascriptIdentifier(exportName) || JavascriptReservedWords.Contains(exportName))
            throw Unsupported("WRK108", declaration);
        _model = model;
        var durableObjectBase = _imports.Require("cloudflare:workers", "DurableObject", "DurableObject");
        _output.Append("export class ").Append(exportName).Append(" extends ").Append(durableObjectBase).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1)
            throw Unsupported("WRK109", constructors[1]);
        var constructor = constructors.SingleOrDefault();
        if (constructor is not null && constructor.ParameterList.Parameters.Count != 2)
            throw Unsupported("WRK109", constructor);
        if (constructor is not null && model.GetDeclaredSymbol(constructor) is IMethodSymbol constructorSymbol
            && (constructorSymbol.Parameters[0].Type.ToDisplayString() != "Workers.DurableObjectState"
                || constructorSymbol.Parameters[1].Type.ToDisplayString() != "Workers.Env"))
            throw Unsupported("WRK109", constructor);
        var stateName = constructor is null ? "state" : ParameterName(constructor.ParameterList.Parameters[0]);
        var envName = constructor is null ? "env" : ParameterName(constructor.ParameterList.Parameters[1]);
        _output.Append("  constructor(").Append(stateName).Append(", ").Append(envName).Append(") { super(")
            .Append(stateName).Append(", ").Append(envName).AppendLine(");");
        _output.Append("    this.state = ").Append(stateName).Append("; this.env = ").Append(envName).AppendLine(";");
        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
            foreach (var variable in field.Declaration.Variables.Where(variable => variable.Initializer is not null))
                _output.Append("    this.").Append(UserIdentifier(model.GetDeclaredSymbol(variable)!, variable.Identifier)).Append(" = ")
                    .Append(Expression(variable.Initializer!.Value)).AppendLine(";");
        if (constructor?.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in constructor?.Body?.Statements ?? []) EmitStatement(statement, 2);
        _output.AppendLine("  }");
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            _model = model;
            var name = method.Identifier.Text switch
            {
                "FetchAsync" => "fetch",
                "AlarmAsync" => "alarm",
                "WebSocketMessageAsync" => "webSocketMessage",
                "WebSocketCloseAsync" => "webSocketClose",
                "WebSocketErrorAsync" => "webSocketError",
                var value => LowerNativeMethodName(value)
            };
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterName));
            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            _output.Append("  ").Append(isAsync ? "async " : "").Append(name).Append('(').Append(parameters).AppendLine(") {");
            if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

    private static bool IsGeneratedInstanceType(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Workers.DurableObjectAttribute")
        || type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Workers.WorkerEntrypointAttribute")
        || type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler";

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
        if (invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null)
            || arguments.Count != method.Parameters.Length)
            throw UnsupportedSymbol(method, invocation);
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
            var parameters = string.Join(", ", declaration.ParameterList.Parameters.Select(ParameterName));
            var isAsync = declaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = symbol.ReturnType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IAsyncEnumerable<T>";
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
