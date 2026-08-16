using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class AspNetHandlerBodyEmitter(AspNetExpressionEmitter expressions)
{
    public string Emit(ExpressionSyntax? expression, BlockSyntax? block)
    {
        if (expression is not null)
            return $"  return {expressions.Response(expression)};\n";
        if (block is null)
            throw new InvalidOperationException("WRK205: This endpoint handler body is not supported.");

        var output = new StringBuilder();
        foreach (var statement in block.Statements)
            output.Append(Statement(statement));
        return output.ToString();
    }

    private string Statement(StatementSyntax statement) => statement switch
    {
        ReturnStatementSyntax { Expression: { } value } => $"  return {expressions.Response(value)};\n",
        LocalDeclarationStatementSyntax local => Local(local),
        ExpressionStatementSyntax expression => $"  {expressions.Value(expression.Expression)};\n",
        IfStatementSyntax conditional => If(conditional),
        ThrowStatementSyntax { Expression: { } value } => $"  throw {expressions.Value(value)};\n",
        _ => throw AspNetDiagnostic.Unsupported("WRK206", statement, "This statement is not supported in ASP.NET handlers.")
    };

    private string Local(LocalDeclarationStatementSyntax declaration)
    {
        if (declaration.Declaration.Variables.Count != 1)
            throw AspNetDiagnostic.Unsupported("WRK206", declaration, "Declare one local variable per statement.");
        var variable = declaration.Declaration.Variables[0];
        if (variable.Initializer is null)
            throw AspNetDiagnostic.Unsupported("WRK206", declaration, "Local variables must have an initializer.");
        expressions.Register(variable);
        return $"  let {expressions.Name(variable)} = {expressions.Value(variable.Initializer.Value)};\n";
    }

    private string If(IfStatementSyntax statement)
    {
        var body = statement.Statement is BlockSyntax block
            ? string.Concat(block.Statements.Select(Statement))
            : Statement(statement.Statement);
        var alternate = statement.Else is null
            ? ""
            : $"  else {{\n{Statement(statement.Else.Statement)}  }}\n";
        return $"  if ({expressions.Value(statement.Condition)}) {{\n{body}  }}\n{alternate}";
    }
}
