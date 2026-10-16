using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string CoalesceAssignment(ICoalesceAssignmentOperation operation)
    {
        var right = ValueExpression(operation.Value);
        if (operation.Target is ILocalReferenceOperation or IParameterReferenceOperation)
            return $"({Expression(operation.Target)} ??= {right})";
        var reference = _names.Get($"coalesce-assignment:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "reference");
        var storage = AssignmentReference(operation.Target);
        if (!ContainsAwait(operation.Value))
            return $"(({reference}) => ({reference}.value ??= {right}))({storage})";
        if (!_expressionTemporaryScopes.TryPeek(out var temporaries)) throw Unsupported("WRK108", operation.Syntax);
        temporaries.Add(reference);
        return $"(({reference} = {storage}), ({reference}.value ??= {right}))";
    }
}
