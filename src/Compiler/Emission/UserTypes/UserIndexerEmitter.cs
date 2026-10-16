using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsUserIndexer(IPropertySymbol property) => property.IsIndexer
        && property.DeclaringSyntaxReferences.Length != 0;

    // An indexer is an accessor call, not a JavaScript property lookup. Keep the
    // captured receiver and arguments together so read/modify/write evaluates them once.
    private string UserIndexerReference(IPropertyReferenceOperation operation, string? receiverOverride = null, string[]? argumentOverrides = null)
    {
        var parameters = operation.Arguments.Select((_, i) => $"index{i}").ToArray();
        var ordered = operation.Arguments.Select((argument, i) => (argument.Parameter!.Ordinal, Value: parameters[i]))
            .OrderBy(item => item.Ordinal).Select(item => item.Value).ToArray();
        var property = operation.Property;
        string Call(bool getter)
        {
            var accessor = PropertyAccessor(property, getter) ?? throw UnsupportedSymbol(property, operation.Syntax);
            var args = getter ? ordered : [.. ordered, "assigned"];
            if (operation.Syntax is ElementAccessExpressionSyntax { Expression: BaseExpressionSyntax })
                return $"{QueueUserType(accessor.ContainingType, operation.Syntax)}.prototype.{UserInstanceMethodName(accessor)}.call(receiver{(args.Length == 0 ? "" : ", " + string.Join(", ", args))})";
            return $"{NotNull("receiver")}.{UserInstanceMethodName(accessor)}({string.Join(", ", args)})";
        }
        var accessors = new List<string>();
        if (PropertyAccessor(property, true) is not null) accessors.Add($"get value() {{ return {Call(true)}; }}");
        if (PropertyAccessor(property, false) is not null) accessors.Add($"set value(assigned) {{ {Call(false)}; }}");
        return $"((receiver{(parameters.Length == 0 ? "" : ", " + string.Join(", ", parameters))}) => ({{ {string.Join(", ", accessors)} }}))"
            + $"({receiverOverride ?? Expression(operation.Instance!)}{(parameters.Length == 0 ? "" : ", " + string.Join(", ", argumentOverrides ?? operation.Arguments.Select(argument => ValueExpression(argument.Value)).ToArray()))})";
    }

    private void EmitUserIndexer(IndexerDeclarationSyntax declaration)
    {
        var property = (IPropertySymbol)_model.GetDeclaredSymbol(declaration)!;
        foreach (var getter in new[] { true, false })
        {
            var accessor = PropertyAccessor(property, getter);
            if (accessor is null || accessor.IsAbstract) continue;
            var parameters = accessor.Parameters.Select(parameter => UserIdentifier(parameter, parameter.Name)).ToArray();
            _output.Append("  ").Append(UserInstanceMethodName(accessor)).Append('(').Append(string.Join(", ", parameters)).AppendLine(") {");
            EmitWithExpressionScope(2, () =>
            {
                if (!SymbolEqualityComparer.Default.Equals(accessor.ContainingType, property.ContainingType))
                    _output.Append("    return ").Append(QueueUserType(accessor.ContainingType, declaration)).Append(".prototype.")
                        .Append(UserInstanceMethodName(accessor)).Append(".call(this")
                        .Append(parameters.Length == 0 ? "" : ", " + string.Join(", ", parameters)).AppendLine(");");
                else
                {
                    var syntax = declaration.AccessorList?.Accessors.SingleOrDefault(item => getter ? item.IsKind(SyntaxKind.GetAccessorDeclaration)
                        : item.IsKind(SyntaxKind.SetAccessorDeclaration) || item.IsKind(SyntaxKind.InitAccessorDeclaration));
                    var expression = getter ? declaration.ExpressionBody?.Expression ?? syntax?.ExpressionBody?.Expression : syntax?.ExpressionBody?.Expression;
                    if (expression is not null) _output.Append("    ").Append(getter ? "return " : "").Append(ValueExpression(expression)).AppendLine(";");
                    else EmitStatements(syntax?.Body?.Statements ?? [], 2);
                }
            });
            _output.AppendLine("  }");
        }
    }

    private void EmitInterfaceIndexer(IPropertySymbol property, IPropertySymbol implementation, SyntaxNode source)
    {
        foreach (var getter in new[] { true, false })
        {
            var accessor = getter ? property.GetMethod : property.SetMethod;
            if (accessor is null) continue;
            var target = PropertyAccessor(implementation, getter) ?? throw UnsupportedSymbol(property, source);
            var args = accessor.Parameters.Select((_, i) => $"arg{i}").ToArray();
            _output.Append("  ").Append(UserInstanceMethodName(accessor)).Append('(').Append(string.Join(", ", args))
                .Append(") { return ").Append(IsSourceInterface(target.ContainingType)
                    ? QueueSourceAccessor(target, source) + ".call(this, " + string.Join(", ", args) + ")"
                    : "this." + UserInstanceMethodName(target) + "(" + string.Join(", ", args) + ")").AppendLine("; }");
        }
    }
}
