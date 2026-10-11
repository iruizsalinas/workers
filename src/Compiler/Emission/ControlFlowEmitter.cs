using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private void EmitTry(ITryOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        _output.Append(indent).AppendLine("try {");
        EmitEmbedded(operation.Body, depth + 1);
        _output.Append(indent).Append('}');

        if (operation.Catches is [{ Filter: null } single] && CatchTypeTest(single, "") is null)
        {
            var variable = CatchVariable(single);
            _output.Append(" catch (").Append(variable).AppendLine(") {");
            EmitCatchBody(single, variable, depth + 1);
            _output.Append(indent).Append('}');
        }
        else if (operation.Catches.Length != 0)
        {
            var source = operation.Syntax;
            var caught = _names.Get($"catch:{source.SyntaxTree.FilePath}:{source.SpanStart}", "exception");
            _output.Append(" catch (").Append(caught).AppendLine(") {");
            var catchAll = false;
            for (var index = 0; index < operation.Catches.Length && !catchAll; index++)
            {
                var clause = operation.Catches[index];
                var variable = CatchVariable(clause);
                var conditions = new List<string>();
                if (CatchTypeTest(clause, caught) is { } typeTest) conditions.Add(typeTest);
                // C# treats an exception thrown by a filter as a failed match.
                if (clause.Filter is not null)
                    conditions.Add($"(({variable}) => {{ try {{ return {Expression(clause.Filter)}; }} catch {{ return false; }} }})({caught})");
                catchAll = conditions.Count == 0;
                _output.Append(indent).Append(index == 0 ? "  " : "  else ")
                    .Append(catchAll ? "" : $"if ({string.Join(" && ", conditions)}) ").AppendLine("{");
                _output.Append(indent).Append("    const ").Append(variable).Append(" = ").Append(caught).AppendLine(";");
                EmitCatchBody(clause, variable, depth + 2);
                _output.Append(indent).AppendLine("  }");
            }
            if (!catchAll) _output.Append(indent).Append("  else throw ").Append(caught).AppendLine(";");
            _output.Append(indent).Append('}');
        }

        if (operation.Finally is not null)
        {
            _output.AppendLine(" finally {");
            EmitEmbedded(operation.Finally, depth + 1);
            _output.Append(indent).Append('}');
        }
        _output.AppendLine();
    }

    private string CatchVariable(ICatchClauseOperation clause) =>
        clause.ExceptionDeclarationOrExpression is IVariableDeclaratorOperation declaration
            ? UserIdentifier(declaration.Symbol, declaration.Symbol.Name)
            : _names.Get($"catch:{clause.Syntax.SyntaxTree.FilePath}:{clause.Syntax.SpanStart}", "exception");

    // Framework exceptions share Error; only user exception classes are distinguishable.
    private string? CatchTypeTest(ICatchClauseOperation clause, string caught)
    {
        var type = clause.ExceptionType;
        if (type is null || type.SpecialType == SpecialType.System_Object || type.ToDisplayString() == "System.Exception") return null;
        if (type.ToDisplayString() == "Workers.HttpError")
            return $"{caught} instanceof {_helpers.Require(JavaScriptHelper.HttpError)}";
        if (type is INamedTypeSymbol named && IsException(named) && IsUserInstanceType(named))
            return $"{caught} instanceof {QueueUserType(named, clause.Syntax)}";
        throw WorkerDiagnostics.WithLocation(new NotSupportedException(
            $"WRK108: Catching '{type}' is not supported; catch System.Exception or an exception type declared in the Worker."), clause.Syntax);
    }

    private void EmitCatchBody(ICatchClauseOperation clause, string variable, int depth)
    {
        _caughtExceptions.Push(variable);
        try { EmitEmbedded(clause.Handler, depth); }
        finally { _caughtExceptions.Pop(); }
    }

    private void EmitSwitch(ISwitchOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        var patterns = operation.Cases.SelectMany(section => section.Clauses)
            .Any(clause => clause is IPatternCaseClauseOperation
                || clause is ISingleValueCaseClauseOperation single && !single.Value.ConstantValue.HasValue);
        var source = operation.Syntax;
        var subject = _names.Get($"switch:{source.SyntaxTree.FilePath}:{source.SpanStart}", "subject");
        if (patterns)
            _output.Append(indent).Append("const ").Append(subject).Append(" = ").Append(Expression(operation.Value)).AppendLine(";");
        _output.Append(indent).Append("switch (").Append(patterns ? "true" : Expression(operation.Value)).AppendLine(") {");
        foreach (var section in operation.Cases)
        {
            foreach (var clause in section.Clauses)
            {
                _output.Append(indent).Append("  ");
                if (clause is IDefaultCaseClauseOperation) _output.AppendLine("default:");
                else
                {
                    var condition = clause switch
                    {
                        ISingleValueCaseClauseOperation single => patterns ? $"{subject} === {Expression(single.Value)}" : Expression(single.Value),
                        IPatternCaseClauseOperation pattern => PatternTest(pattern.Pattern, subject)
                            + (pattern.Guard is null ? "" : $" && ({Expression(pattern.Guard)})"),
                        _ => throw Unsupported("WRK100", clause.Syntax)
                    };
                    _output.Append("case ").Append(condition).AppendLine(":");
                }
            }
            EmitStatements(section.Body, depth + 2);
        }
        _output.Append(indent).AppendLine("}");
    }
}
