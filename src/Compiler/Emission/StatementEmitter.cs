using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private void EmitStatement(StatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        switch (statement)
        {
            case ReturnStatementSyntax value:
                _output.Append(indent).Append("return");
                if (value.Expression is not null) _output.Append(' ').Append(Expression(value.Expression));
                _output.AppendLine(";");
                break;
            case LocalDeclarationStatementSyntax local:
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
                if (BindingIntrinsicRegistry.IsQueueMessageBatch(_model.GetTypeInfo(loop.Expression).Type))
                    enumerable += ".messages";
                _output.Append(indent)
                    .Append("for (const ")
                    .Append(UserIdentifier(_model.GetDeclaredSymbol(loop)!, loop.Identifier))
                    .Append(" of ")
                    .Append(enumerable)
                    .AppendLine(") {");
                EmitEmbedded(loop.Statement, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            default:
                throw Unsupported("WRK100", statement);
        }
    }

    private void EmitEmbedded(StatementSyntax statement, int depth)
    {
        if (statement is BlockSyntax block) foreach (var child in block.Statements) EmitStatement(child, depth);
        else EmitStatement(statement, depth);
    }
}
