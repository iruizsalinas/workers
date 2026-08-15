using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private void EmitTry(TryStatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        _output.Append(indent).AppendLine("try {");
        EmitEmbedded(statement.Block, depth + 1);
        _output.Append(indent).Append('}');

        foreach (var clause in statement.Catches)
        {
            if (clause.Filter is not null)
                throw Unsupported("WRK100", clause.Filter);
            var variable = clause.Declaration?.Identifier is { RawKind: not 0 } identifier
                ? UserIdentifier(_model.GetDeclaredSymbol(clause.Declaration!)!, identifier)
                : null;
            _output.Append(" catch");
            if (variable is not null) _output.Append(" (").Append(variable).Append(')');
            _output.AppendLine(" {");
            EmitEmbedded(clause.Block, depth + 1);
            _output.Append(indent).Append('}');
        }

        if (statement.Finally is not null)
        {
            _output.AppendLine(" finally {");
            EmitEmbedded(statement.Finally.Block, depth + 1);
            _output.Append(indent).Append('}');
        }

        _output.AppendLine();
    }

    private void EmitSwitch(SwitchStatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        _output.Append(indent).Append("switch (").Append(Expression(statement.Expression)).AppendLine(") {");
        foreach (var section in statement.Sections)
        {
            foreach (var label in section.Labels)
            {
                _output.Append(indent).Append("  ");
                if (label is DefaultSwitchLabelSyntax)
                    _output.AppendLine("default:");
                else if (label is CaseSwitchLabelSyntax @case)
                    _output.Append("case ").Append(Expression(@case.Value)).AppendLine(":");
                else
                    throw Unsupported("WRK100", label);
            }
            foreach (var child in section.Statements)
                EmitStatement(child, depth + 2);
        }
        _output.Append(indent).AppendLine("}");
    }
}
