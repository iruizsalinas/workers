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
                else if (IsDictionary(_model.GetTypeInfo(loop.Expression).Type))
                    enumerable = $"Object.entries({enumerable})";
                _output.Append(indent)
                    .Append("for (const ")
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
        if (statement is BlockSyntax block) foreach (var child in block.Statements) EmitStatement(child, depth);
        else EmitStatement(statement, depth);
    }
}
