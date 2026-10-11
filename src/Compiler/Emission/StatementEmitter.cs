using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private void EmitStatement(StatementSyntax statement, int depth) =>
        EmitStatement(_model.GetOperation(statement) ?? throw Unsupported("WRK100", statement), depth);

    private void EmitStatement(IOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        _diagnosticNode = operation.Syntax;
        if (operation.Syntax is StatementSyntax statement)
        {
            EmitOutVariableDeclarations(statement, indent);
            DeclarePatternVariables(StatementPatternDesignations(statement),
                name => _output.Append(indent).Append("let ").Append(name).AppendLine(";"));
        }
        switch (operation)
        {
            case IReturnOperation value when value.Kind == OperationKind.YieldReturn:
                _output.Append(indent).Append("yield ").Append(Expression(value.ReturnedValue!)).AppendLine(";");
                break;
            case IReturnOperation value when value.Kind == OperationKind.Return:
                _output.Append(indent).Append("return");
                if (value.ReturnedValue is not null) _output.Append(' ').Append(Expression(value.ReturnedValue));
                _output.AppendLine(";");
                break;
            case IVariableDeclarationGroupOperation declarations:
                foreach (var variable in declarations.Declarations.SelectMany(declaration => declaration.Declarators))
                    _output.Append(indent).Append("let ").Append(VariableDeclaration(variable)).AppendLine(";");
                break;
            case IExpressionStatementOperation expression:
                _output.Append(indent).Append(Expression(expression.Operation)).AppendLine(";");
                break;
            case IConditionalOperation conditional:
                _output.Append(indent).Append("if (").Append(Expression(conditional.Condition)).AppendLine(") {");
                EmitEmbedded(conditional.WhenTrue, depth + 1);
                _output.Append(indent).Append('}');
                if (conditional.WhenFalse is not null)
                {
                    _output.AppendLine(" else {");
                    EmitEmbedded(conditional.WhenFalse, depth + 1);
                    _output.Append(indent).Append('}');
                }
                _output.AppendLine();
                break;
            case IForEachLoopOperation loop:
                var collection = loop.Collection;
                while (collection is IConversionOperation { IsImplicit: true } conversion) collection = conversion.Operand;
                var enumerable = Expression(collection);
                if (collection.Type?.SpecialType == SpecialType.System_String)
                    enumerable = $"{_helpers.Require(JavaScriptHelper.LinqValues)}({enumerable})";
                else if (BindingIntrinsicRegistry.IsQueueMessageBatch(collection.Type)) enumerable += ".messages";
                else if (IsDictionary(collection.Type)) enumerable = $"Object.entries({enumerable})";
                if (loop.Locals.Length != 1 || loop.LoopControlVariable is not IVariableDeclaratorOperation)
                    throw Unsupported("WRK108", loop.Syntax);
                _output.Append(indent).Append(loop.IsAsynchronous ? "for await (const " : "for (const ")
                    .Append(UserIdentifier(loop.Locals[0], loop.Locals[0].Name)).Append(" of ").Append(enumerable).AppendLine(") {");
                EmitEmbedded(loop.Body, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case ITryOperation value:
                EmitTry(value, depth);
                break;
            case ISwitchOperation value:
                EmitSwitch(value, depth);
                break;
            case IBranchOperation { BranchKind: BranchKind.Break }:
                _output.Append(indent).AppendLine("break;");
                break;
            case IBranchOperation { BranchKind: BranchKind.Continue }:
                _output.Append(indent).AppendLine("continue;");
                break;
            case IThrowOperation value:
                var exception = value.Exception is not null ? Expression(value.Exception)
                    : _caughtExceptions.TryPeek(out var caught) ? caught : throw Unsupported("WRK108", value.Syntax);
                _output.Append(indent).Append("throw ").Append(exception).AppendLine(";");
                break;
            case IWhileLoopOperation { ConditionIsTop: false } loop:
                _output.Append(indent).AppendLine("do {");
                EmitEmbedded(loop.Body, depth + 1);
                _output.Append(indent).Append("} while (").Append(Expression(loop.Condition!)).AppendLine(");");
                break;
            case IWhileLoopOperation loop:
                _output.Append(indent).Append("while (").Append(Expression(loop.Condition!)).AppendLine(") {");
                EmitEmbedded(loop.Body, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IForLoopOperation loop:
                _output.Append(indent).Append("for (").Append(string.Join(", ", loop.Before.Select(ForInitializer))).Append("; ")
                    .Append(loop.Condition is null ? "" : Expression(loop.Condition)).Append("; ")
                    .Append(string.Join(", ", loop.AtLoopBottom.Select(ForInitializer))).AppendLine(") {");
                EmitEmbedded(loop.Body, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IUsingOperation { Resources: IVariableDeclarationGroupOperation } value:
                var source = (UsingStatementSyntax)value.Syntax;
                _output.Append(indent).AppendLine("{");
                EmitUsing(source.Declaration!, value.IsAsynchronous, body => EmitEmbedded(value.Body, body), depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IBlockOperation block:
                _output.Append(indent).AppendLine("{");
                EmitStatements(block.Operations, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IEmptyOperation:
                break;
            default:
                throw Unsupported("WRK100", operation.Syntax);
        }
    }

    private string VariableDeclaration(IVariableDeclaratorOperation variable) =>
        UserIdentifier(variable.Symbol, variable.Symbol.Name)
        + (variable.Initializer is null ? "" : " = " + Expression(variable.Initializer.Value));

    private string ForInitializer(IOperation operation) => operation switch
    {
        IVariableDeclarationGroupOperation group => "let " + string.Join(", ", group.Declarations
            .SelectMany(declaration => declaration.Declarators).Select(VariableDeclaration)),
        IExpressionStatementOperation expression => Expression(expression.Operation),
        _ => throw Unsupported("WRK100", operation.Syntax)
    };

    private void EmitEmbedded(StatementSyntax statement, int depth) =>
        EmitEmbedded(_model.GetOperation(statement) ?? throw Unsupported("WRK100", statement), depth);

    private void EmitEmbedded(IOperation operation, int depth)
    {
        if (operation is IBlockOperation block) EmitStatements(block.Operations, depth);
        else EmitStatement(operation, depth);
    }

    private void EmitStatements(IReadOnlyList<StatementSyntax> statements, int depth) =>
        EmitStatements(statements.Select(statement => _model.GetOperation(statement)
            ?? throw Unsupported("WRK100", statement)).ToArray(), depth);

    // Using declarations own the remaining operations in their enclosing block.
    private void EmitStatements(IReadOnlyList<IOperation> statements, int depth)
    {
        for (var index = 0; index < statements.Count; index++)
        {
            if (statements[index] is IUsingDeclarationOperation resource)
            {
                _diagnosticNode = resource.Syntax;
                var local = (LocalDeclarationStatementSyntax)resource.Syntax;
                var rest = statements.Skip(index + 1).ToArray();
                EmitUsing(local.Declaration, resource.IsAsynchronous, body => EmitStatements(rest, body), depth);
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
        var operation = (IVariableDeclaratorOperation)_model.GetOperation(variable)!;
        var local = operation.Symbol;
        var name = UserIdentifier(local, local.Name);
        _output.Append(indent).Append("const ").Append(name).Append(" = ")
            .Append(Expression(operation.Initializer!.Value)).AppendLine(";");
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
        throw Unsupported("WRK108", source);
    }

    private IMethodSymbol? DisposeMethod(ITypeSymbol type, string interfaceName, string methodName) =>
        _compilation.GetTypeByMetadataName(interfaceName)?.GetMembers(methodName).SingleOrDefault() is { } member
            ? type.FindImplementationForInterfaceMember(member) as IMethodSymbol
            : null;
}
