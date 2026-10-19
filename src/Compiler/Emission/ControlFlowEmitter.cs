using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<ILabelSymbol, string> _loopContinueTargets = new(SymbolEqualityComparer.Default);

    private void EmitWhile(IWhileLoopOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        var variables = new List<string>();
        DeclareScopeVariables(operation.Condition!.Syntax, variables.Add);
        if (variables.Count == 0)
        {
            if (operation.ConditionIsTop)
                _output.Append(indent).Append("while (").Append(Expression(operation.Condition)).AppendLine(") {");
            else _output.Append(indent).AppendLine("do {");
            EmitEmbedded(operation.Body, depth + 1);
            if (operation.ConditionIsTop) _output.Append(indent).AppendLine("}");
            else _output.Append(indent).Append("} while (").Append(Expression(operation.Condition)).AppendLine(");");
            return;
        }
        _output.Append(indent).AppendLine("while (true) {");
        if (!operation.ConditionIsTop) EmitContinueBlock(operation, depth + 1);
        _output.Append(indent).Append("  let ").Append(string.Join(", ", variables)).AppendLine(";");
        _output.Append(indent).Append("  if (!(").Append(Expression(operation.Condition)).AppendLine(")) break;");
        if (operation.ConditionIsTop) EmitEmbedded(operation.Body, depth + 1);
        _output.Append(indent).AppendLine("}");
    }

    private void EmitFor(IForLoopOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        // Initializer locals are shared; condition and incrementor locals are fresh each iteration.
        _output.Append(indent).AppendLine("{");
        foreach (var initializer in operation.Before)
        {
            EmitScopeVariableDeclarations(initializer.Syntax, indent + "  ");
            EmitStatement(initializer, depth + 1);
        }
        var variables = new List<string>();
        if (operation.Condition is not null) DeclareScopeVariables(operation.Condition.Syntax, variables.Add);
        foreach (var increment in operation.AtLoopBottom) DeclareScopeVariables(increment.Syntax, variables.Add);
        if (variables.Count == 0)
        {
            _output.Append(indent).Append("  for (; ")
                .Append(operation.Condition is null ? "" : Expression(operation.Condition)).Append("; ")
                .Append(string.Join(", ", operation.AtLoopBottom.Select(ForInitializer))).AppendLine(") {");
            EmitEmbedded(operation.Body, depth + 2);
        }
        else
        {
            _output.Append(indent).AppendLine("  for (;;) {");
            _output.Append(indent).Append("    let ").Append(string.Join(", ", variables)).AppendLine(";");
            if (operation.Condition is not null)
                _output.Append(indent).Append("    if (!(").Append(Expression(operation.Condition)).AppendLine(")) break;");
            EmitContinueBlock(operation, depth + 2);
            foreach (var increment in operation.AtLoopBottom)
                _output.Append(indent).Append("    ").Append(ForInitializer(increment)).AppendLine(";");
        }
        _output.Append(indent).AppendLine("  }");
        _output.Append(indent).AppendLine("}");
    }

    private void EmitContinueBlock(ILoopOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        var label = _names.Get($"loop-continue:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "loopBody");
        _output.Append(indent).Append(label).AppendLine(": {");
        _loopContinueTargets.Add(operation.ContinueLabel, label);
        try { EmitEmbedded(operation.Body, depth + 1); }
        finally { _loopContinueTargets.Remove(operation.ContinueLabel); }
        _output.Append(indent).AppendLine("}");
    }

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
            var original = variable;
            if (single.ExceptionDeclarationOrExpression is not null)
            {
                original = _names.Get($"rethrow:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "originalException");
                _output.Append(indent).Append("  const ").Append(original).Append(" = ").Append(variable).AppendLine(";");
            }
            EmitCatchBody(single, original, depth + 1);
            _output.Append(indent).Append('}');
        }
        else if (operation.Catches.Length != 0)
        {
            var source = operation.Syntax;
            var caught = _names.Get($"catch:{source.SyntaxTree.FilePath}:{source.SpanStart}", "exception");
            _output.Append(" catch (").Append(caught).AppendLine(") {");
            EmitCatchClauses(operation.Catches, caught, 0, depth + 1);
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

    private void EmitCatchClauses(System.Collections.Immutable.ImmutableArray<ICatchClauseOperation> clauses, string caught, int index, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (index == clauses.Length)
        {
            _output.Append(indent).Append("throw ").Append(caught).AppendLine(";");
            return;
        }
        var clause = clauses[index];
        var variable = CatchVariable(clause);
        _output.Append(indent).Append("let ").Append(variable).Append(" = ").Append(caught).AppendLine(";");
        EmitScopeVariableDeclarations(clause.Syntax, indent);
        var conditions = new List<string>();
        if (CatchTypeTest(clause, caught) is { } typeTest) conditions.Add(typeTest);
        // Share the catch binding with its filter, while treating a thrown filter as false.
        if (clause.Filter is not null)
            conditions.Add($"(() => {{ try {{ return {Expression(clause.Filter)}; }} catch {{ return false; }} }})()");
        if (conditions.Count == 0)
        {
            EmitCatchBody(clause, caught, depth);
            return;
        }
        _output.Append(indent).Append("if (").Append(string.Join(" && ", conditions)).AppendLine(") {");
        EmitCatchBody(clause, caught, depth + 1);
        _output.Append(indent).AppendLine("} else {");
        EmitCatchClauses(clauses, caught, index + 1, depth + 1);
        _output.Append(indent).AppendLine("}");
    }

    private string CatchVariable(ICatchClauseOperation clause) =>
        clause.ExceptionDeclarationOrExpression is IVariableDeclaratorOperation declaration
            ? UserIdentifier(declaration.Symbol, declaration.Symbol.Name)
            : _names.Get($"catch:{clause.Syntax.SyntaxTree.FilePath}:{clause.Syntax.SpanStart}", "exception");

    // Typed exceptions use their Roslyn inheritance chain, shared by helper failures.
    private string? CatchTypeTest(ICatchClauseOperation clause, string caught)
    {
        var type = clause.ExceptionType;
        if (type is null || type.SpecialType == SpecialType.System_Object || type.ToDisplayString() == "System.Exception") return null;
        if (type.ToDisplayString() == "Workers.HttpError")
            return $"{caught} instanceof {_helpers.Require(JavaScriptHelper.HttpError)}";
        if (type is INamedTypeSymbol named && IsException(named) && IsUserInstanceType(named))
            return $"{caught} instanceof {QueueUserType(named, clause.Syntax)}";
        if (type is INamedTypeSymbol builtIn && IsException(builtIn) && builtIn.DeclaringSyntaxReferences.Length == 0)
            return $"{caught} instanceof ({BuiltInExceptionType(builtIn)})";
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
            _output.Append(indent).Append("const ").Append(subject).Append(" = ").Append(ValueExpression(operation.Value)).AppendLine(";");
        _output.Append(indent).Append("switch (").Append(patterns ? "true" : Expression(operation.Value)).AppendLine(") {");
        // Local functions declared directly under case labels share the switch block's scope.
        var frame = new LocalFunctionFrame(operation.Cases.SelectMany(section => section.Body).OfType<ILocalFunctionOperation>());
        _localFunctionFrames.Push(frame);
        try
        {
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
                EmitStatements(section.Body.Where(statement => statement is not ILocalFunctionOperation).ToArray(), depth + 2);
            }
            EmitLocalFunctions(frame, depth + 1);
        }
        finally { _localFunctionFrames.Pop(); }
        _output.Append(indent).AppendLine("}");
    }
}
