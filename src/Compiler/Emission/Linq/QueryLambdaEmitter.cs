using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    // Anonymous methods and query lambdas use their fully bound Roslyn bodies.
    private string BoundQueryLambda(IAnonymousFunctionOperation operation)
    {
        var start = _output.Length;
        try
        {
            _output.Append(operation.Symbol.IsAsync ? "async (" : "(").Append(string.Join(", ", operation.Symbol.Parameters.Select(parameter => UserIdentifier(parameter, parameter.Name))))
                .AppendLine(") => {");
            EmitWithExpressionScope(1, () => EmitStatements(operation.Body.Operations, 1));
            _output.Append('}');
            return _output.ToString(start, _output.Length - start);
        }
        finally { _output.Length = start; }
    }
}
