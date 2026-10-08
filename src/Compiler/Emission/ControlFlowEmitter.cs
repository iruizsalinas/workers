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

        if (statement.Catches is [{ Filter: null } single] && CatchTypeTest(single, "") is null)
        {
            var variable = CatchVariable(single);
            _output.Append(" catch (").Append(variable).AppendLine(") {");
            EmitCatchBody(single, variable, depth + 1);
            _output.Append(indent).Append('}');
        }
        else if (statement.Catches.Count != 0)
        {
            var caught = _names.Get($"catch:{statement.SyntaxTree.FilePath}:{statement.SpanStart}", "exception");
            _output.Append(" catch (").Append(caught).AppendLine(") {");
            var catchAll = false;
            for (var index = 0; index < statement.Catches.Count && !catchAll; index++)
            {
                var clause = statement.Catches[index];
                var variable = CatchVariable(clause);
                var conditions = new List<string>();
                if (CatchTypeTest(clause, caught) is { } typeTest) conditions.Add(typeTest);
                if (clause.Filter is not null)
                    conditions.Add($"(({variable}) => {Expression(clause.Filter.FilterExpression)})({caught})");
                catchAll = conditions.Count == 0;
                _output.Append(indent).Append(index == 0 ? "  " : "  else ")
                    .Append(catchAll ? "" : $"if ({string.Join(" && ", conditions)}) ").AppendLine("{");
                _output.Append(indent).Append("    const ").Append(variable).Append(" = ").Append(caught).AppendLine(";");
                EmitCatchBody(clause, variable, depth + 2);
                _output.Append(indent).AppendLine("  }");
            }
            if (!catchAll)
                _output.Append(indent).Append("  else throw ").Append(caught).AppendLine(";");
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

    // Pattern case labels become conditions of switch (true), which keeps the section and break
    // semantics of the C# switch statement while testing labels in source order.
    private void EmitPatternSwitch(SwitchStatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        var subject = _names.Get($"switch:{statement.SyntaxTree.FilePath}:{statement.SpanStart}", "subject");
        var input = _model.GetTypeInfo(statement.Expression).Type;
        _output.Append(indent).Append("const ").Append(subject).Append(" = ").Append(Expression(statement.Expression)).AppendLine(";");
        _output.Append(indent).AppendLine("switch (true) {");
        foreach (var section in statement.Sections)
        {
            foreach (var label in section.Labels)
            {
                _output.Append(indent).Append("  ");
                if (label is DefaultSwitchLabelSyntax)
                {
                    _output.AppendLine("default:");
                    continue;
                }
                var condition = label switch
                {
                    CasePatternSwitchLabelSyntax pattern => PatternTest(pattern.Pattern, subject, input)
                        + (pattern.WhenClause is null ? "" : $" && ({Expression(pattern.WhenClause.Condition)})"),
                    CaseSwitchLabelSyntax @case => $"{subject} === {Expression(@case.Value)}",
                    _ => throw Unsupported("WRK100", label)
                };
                _output.Append("case ").Append(condition).AppendLine(":");
            }
            foreach (var child in section.Statements)
                EmitStatement(child, depth + 2);
        }
        _output.Append(indent).AppendLine("}");
    }

    private string CatchVariable(CatchClauseSyntax clause) =>
        clause.Declaration?.Identifier is { RawKind: not 0 } identifier
            ? UserIdentifier(_model.GetDeclaredSymbol(clause.Declaration!)!, identifier)
            : _names.Get($"catch:{clause.SyntaxTree.FilePath}:{clause.SpanStart}", "exception");

    // Returns null when the clause catches every exception. Only user exception types have a
    // distinguishable JavaScript class; framework exception types all lower to Error.
    private string? CatchTypeTest(CatchClauseSyntax clause, string caught)
    {
        if (clause.Declaration is null) return null;
        var type = _model.GetTypeInfo(clause.Declaration.Type).Type as INamedTypeSymbol;
        if (type?.ToDisplayString() == "System.Exception") return null;
        if (type is not null && IsException(type) && IsUserInstanceType(type))
            return $"{caught} instanceof {QueueUserType(type, clause.Declaration)}";
        throw new NotSupportedException(
            $"WRK108: Catching '{type}' is not supported; catch System.Exception or an exception type declared in the Worker.");
    }

    private void EmitCatchBody(CatchClauseSyntax clause, string variable, int depth)
    {
        _caughtExceptions.Push(variable);
        try
        {
            EmitEmbedded(clause.Block, depth);
        }
        finally
        {
            _caughtExceptions.Pop();
        }
    }

    private void EmitSwitch(SwitchStatementSyntax statement, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (statement.Sections.SelectMany(section => section.Labels).Any(label => label is CasePatternSwitchLabelSyntax
                || label is CaseSwitchLabelSyntax @case && !_model.GetConstantValue(@case.Value).HasValue))
        {
            EmitPatternSwitch(statement, depth);
            return;
        }
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
