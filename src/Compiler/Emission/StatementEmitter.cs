using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private void EmitStatement(StatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        _diagnosticNode = statement;
        EmitOutVariableDeclarations(statement, indent);
        DeclarePatternVariables(StatementPatternDesignations(statement),
            name => _output.Append(indent).Append("let ").Append(name).AppendLine(";"));
        switch (statement)
        {
            case ReturnStatementSyntax value:
                _output.Append(indent).Append("return");
                if (value.Expression is not null) _output.Append(' ').Append(Expression(value.Expression));
                _output.AppendLine(";");
                break;
            case LocalDeclarationStatementSyntax local:
                if (!local.UsingKeyword.IsKind(SyntaxKind.None))
                    throw Unsupported("WRK108", local);
                foreach (var variable in local.Declaration.Variables)
                    _output.Append(indent).Append("let ").Append(UserIdentifier(_model.GetDeclaredSymbol(variable)!, variable.Identifier))
                        .Append(variable.Initializer is null ? "" : " = " + Expression(variable.Initializer.Value)).AppendLine(";");
                break;
            case ExpressionStatementSyntax expression:
                _output.Append(indent).Append(Expression(expression.Expression)).AppendLine(";");
                break;
            case IfStatementSyntax conditional:
                _output.Append(indent).Append("if (").Append(Expression(conditional.Condition)).AppendLine(") {");
                EmitEmbedded(conditional.Statement, depth + 1);
                _output.Append(indent).Append('}');
                if (conditional.Else is not null) { _output.AppendLine(" else {"); EmitEmbedded(conditional.Else.Statement, depth + 1); _output.Append(indent).Append('}'); }
                _output.AppendLine();
                break;
            case ForEachStatementSyntax loop:
                var enumerable = Expression(loop.Expression);
                var enumerableType = _model.GetTypeInfo(loop.Expression).Type;
                if (enumerableType?.SpecialType == SpecialType.System_String)
                    enumerable = $"{_helpers.Require(JavaScriptHelper.LinqValues)}({enumerable})";
                else if (BindingIntrinsicRegistry.IsQueueMessageBatch(enumerableType))
                    enumerable += ".messages";
                else if (IsDictionary(enumerableType))
                    enumerable = $"Object.entries({enumerable})";
                _output.Append(indent)
                    .Append(loop.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword) ? "for await (const " : "for (const ")
                    .Append(UserIdentifier(_model.GetDeclaredSymbol(loop)!, loop.Identifier))
                    .Append(" of ")
                    .Append(enumerable)
                    .AppendLine(") {");
                EmitEmbedded(loop.Statement, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case TryStatementSyntax value:
                EmitTry(value, depth);
                break;
            case SwitchStatementSyntax value:
                EmitSwitch(value, depth);
                break;
            case BreakStatementSyntax:
                _output.Append(indent).AppendLine("break;");
                break;
            case ThrowStatementSyntax value:
                _output.Append(indent).Append("throw");
                if (value.Expression is not null)
                    _output.Append(' ').Append(Expression(value.Expression));
                else if (_caughtExceptions.TryPeek(out var exception))
                    _output.Append(' ').Append(exception);
                else
                    throw Unsupported("WRK108", value);
                _output.AppendLine(";");
                break;
            case DoStatementSyntax value:
                _output.Append(indent).AppendLine("do {");
                EmitEmbedded(value.Statement, depth + 1);
                _output.Append(indent).Append("} while (").Append(Expression(value.Condition)).AppendLine(");");
                break;
            case WhileStatementSyntax value:
                _output.Append(indent).Append("while (").Append(Expression(value.Condition)).AppendLine(") {");
                EmitEmbedded(value.Statement, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case ForStatementSyntax value:
                var declaration = value.Declaration is null ? "" : string.Join(", ", value.Declaration.Variables.Select(variable =>
                    variable.Initializer is null
                        ? UserIdentifier(_model.GetDeclaredSymbol(variable)!, variable.Identifier)
                        : $"{UserIdentifier(_model.GetDeclaredSymbol(variable)!, variable.Identifier)} = {Expression(variable.Initializer.Value)}"));
                var initializers = value.Declaration is null
                    ? string.Join(", ", value.Initializers.Select(Expression))
                    : $"let {declaration}";
                _output.Append(indent).Append("for (").Append(initializers).Append("; ")
                    .Append(value.Condition is null ? "" : Expression(value.Condition)).Append("; ")
                    .Append(string.Join(", ", value.Incrementors.Select(Expression))).AppendLine(") {");
                EmitEmbedded(value.Statement, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case UsingStatementSyntax { Declaration: { } resources } value:
                _output.Append(indent).AppendLine("{");
                EmitUsing(resources, value.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword),
                    body => EmitEmbedded(value.Statement, body), depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case ContinueStatementSyntax:
                _output.Append(indent).AppendLine("continue;");
                break;
            case YieldStatementSyntax value when value.IsKind(SyntaxKind.YieldReturnStatement):
                _output.Append(indent).Append("yield ").Append(Expression(value.Expression!)).AppendLine(";");
                break;
            default:
                throw Unsupported("WRK100", statement);
        }
    }

    private void EmitEmbedded(StatementSyntax statement, int depth)
    {
        if (statement is BlockSyntax block) EmitStatements(block.Statements, depth);
        else EmitStatement(statement, depth);
    }

    // A using declaration owns the rest of its block, so the remaining statements become the
    // body of the try whose finally disposes the resource.
    private void EmitStatements(IReadOnlyList<StatementSyntax> statements, int depth)
    {
        for (var index = 0; index < statements.Count; index++)
        {
            if (statements[index] is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0 } local)
            {
                var rest = statements.Skip(index + 1).ToArray();
                EmitUsing(local.Declaration, local.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword),
                    body => EmitStatements(rest, body), depth);
                return;
            }
            EmitStatement(statements[index], depth);
        }
    }

    private void EmitUsing(VariableDeclarationSyntax declaration, bool isAsync, Action<int> body, int depth, int index = 0)
    {
        if (index == declaration.Variables.Count)
        {
            body(depth);
            return;
        }
        var indent = new string(' ', depth * 2);
        var variable = declaration.Variables[index];
        var local = (ILocalSymbol)_model.GetDeclaredSymbol(variable)!;
        var name = UserIdentifier(local, variable.Identifier);
        _output.Append(indent).Append("const ").Append(name).Append(" = ")
            .Append(Expression(variable.Initializer!.Value)).AppendLine(";");
        var dispose = DisposeInvocation(local.Type, name, isAsync, variable);
        _output.Append(indent).AppendLine("try {");
        EmitUsing(declaration, isAsync, body, depth + 1, index + 1);
        _output.Append(indent).AppendLine("} finally {");
        _output.Append(indent).Append("  if (").Append(name).Append(" != null) ")
            .Append(isAsync ? "await " : "").Append(dispose).AppendLine(";");
        _output.Append(indent).AppendLine("}");
    }

    private string DisposeInvocation(ITypeSymbol type, string receiver, bool isAsync, SyntaxNode source)
    {
        var method = (isAsync ? DisposeMethod(type, "System.IAsyncDisposable", "DisposeAsync") : null)
            ?? DisposeMethod(type, "System.IDisposable", "Dispose");
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic))
            return EmitBindingIntrinsic(receiver, method, intrinsic, []);
        if (method is { IsStatic: false } && IsUserInstanceType(method.ContainingType))
            return $"{receiver}.{UserInstanceMethodName(method)}()";
        if (method?.ContainingType.ToDisplayString() == "System.Threading.CancellationTokenSource")
            return HelperInvocation(JavaScriptHelper.CancellationCancelAfter, [receiver, "-1"]);
        throw Unsupported("WRK108", source);
    }

    private IMethodSymbol? DisposeMethod(ITypeSymbol type, string interfaceName, string methodName) =>
        _compilation.GetTypeByMetadataName(interfaceName)?.GetMembers(methodName).SingleOrDefault() is { } member
            ? type.FindImplementationForInterfaceMember(member) as IMethodSymbol
            : null;
}
