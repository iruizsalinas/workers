using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private sealed class LocalFunctionFrame(IEnumerable<ILocalFunctionOperation> declarations)
    {
        public readonly Dictionary<IMethodSymbol, ILocalFunctionOperation> Declarations =
            declarations.ToDictionary<ILocalFunctionOperation, IMethodSymbol, ILocalFunctionOperation>(
                item => item.Symbol.OriginalDefinition, item => item, SymbolEqualityComparer.Default);
        public readonly Dictionary<IMethodSymbol, string> Names = new(SymbolEqualityComparer.Default);
        public readonly Queue<IMethodSymbol> Pending = new();
    }

    private readonly Stack<LocalFunctionFrame> _localFunctionFrames = new();

    private string LocalDelegateIdentity(IMethodSymbol method, SyntaxNode source, out bool capturesThis, out bool capturesValues)
    {
        var declaration = (LocalFunctionStatementSyntax)method.DeclaringSyntaxReferences[0].GetSyntax();
        var syntax = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody!.Expression;
        var flow = _model.AnalyzeDataFlow(syntax) ?? throw UnsupportedSymbol(method, source);
        var declared = flow.VariablesDeclared.Cast<ISymbol>().Concat(method.OriginalDefinition.Parameters).ToHashSet(SymbolEqualityComparer.Default);
        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var captures = Gather(method).Where(symbol => !declared.Contains(symbol)).Distinct(SymbolEqualityComparer.Default).ToArray();
        capturesThis = captures.Any(symbol => symbol is IParameterSymbol { IsThis: true }
            || symbol is IParameterSymbol parameter && IsClassPrimaryParameter(parameter));
        capturesValues = captures.Any(symbol => symbol is not IParameterSymbol { IsThis: true }
            && !(symbol is IParameterSymbol parameter && IsClassPrimaryParameter(parameter)));
        if (capturesValues) return LocalFunctionTarget(method, source);
        var specialization = string.Join(";", _typeArguments.OrderBy(pair => pair.Key.ToDisplayString(), StringComparer.Ordinal)
            .Select(pair => pair.Key.ToDisplayString() + "=" + RuntimeTypeName(ResolveType(pair.Value))));
        return System.Text.Json.JsonSerializer.Serialize($"local:{declaration.SyntaxTree.FilePath}:{declaration.SpanStart}:{RuntimeTypeName(method.ContainingType)}:{method}:{specialization}");

        IEnumerable<ISymbol> Gather(IMethodSymbol callable)
        {
            callable = callable.OriginalDefinition;
            if (!seen.Add(callable)) yield break;
            var local = (LocalFunctionStatementSyntax)callable.DeclaringSyntaxReferences[0].GetSyntax();
            var body = (SyntaxNode?)local.Body ?? local.ExpressionBody!.Expression;
            var analysis = _model.AnalyzeDataFlow(body) ?? throw UnsupportedSymbol(callable, source);
            var locals = analysis.VariablesDeclared.Cast<ISymbol>().Concat(callable.Parameters).ToHashSet(SymbolEqualityComparer.Default);
            foreach (var capture in analysis.CapturedInside.Where(symbol => !locals.Contains(symbol))) yield return capture;
            foreach (var dependency in Dependencies(_model.GetOperation(body)!))
                foreach (var capture in Gather(dependency)) yield return capture;
        }

        static IEnumerable<IMethodSymbol> Dependencies(IOperation operation)
        {
            if (operation is IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction } call) yield return call.TargetMethod;
            if (operation is IMethodReferenceOperation { Method.MethodKind: MethodKind.LocalFunction } reference) yield return reference.Method;
            foreach (var child in operation.ChildOperations)
                foreach (var dependency in Dependencies(child)) yield return dependency;
        }
    }

    private string LocalFunctionTarget(IMethodSymbol method, SyntaxNode source)
    {
        method = ResolveMethod(method);
        var frame = _localFunctionFrames.FirstOrDefault(item => item.Declarations.ContainsKey(method.OriginalDefinition))
            ?? throw UnsupportedSymbol(method, source);
        if (frame.Names.TryGetValue(method, out var name)) return name;
        CountSpecialization(method, source);
        name = _names.Get($"local-function:{source.SyntaxTree.FilePath}:{method.DeclaringSyntaxReferences[0].Span.Start}:{method.ToDisplayString()}", method.Name);
        frame.Names.Add(method, name);
        frame.Pending.Enqueue(method);
        return name;
    }

    private void EmitLocalFunctions(LocalFunctionFrame frame, int depth)
    {
        while (frame.Pending.TryDequeue(out var method))
        {
            if (method.Parameters.Any(parameter => parameter.RefKind is not (RefKind.None or RefKind.Out)
                || parameter.IsParams && parameter.Type is not IArrayTypeSymbol)) throw UnsupportedSymbol(method, frame.Declarations[method.OriginalDefinition].Syntax);
            var declaration = (LocalFunctionStatementSyntax)frame.Declarations[method.OriginalDefinition].Syntax;
            var previousArguments = _typeArguments;
            var previousOut = _declaredOutVariables;
            var previousPatterns = _declaredPatternVariables;
            _typeArguments = new(previousArguments, SymbolEqualityComparer.Default);
            _declaredOutVariables = new(SymbolEqualityComparer.Default);
            _declaredPatternVariables = new(SymbolEqualityComparer.Default);
            for (var index = 0; index < method.Arity; index++)
                _typeArguments[method.OriginalDefinition.TypeParameters[index]] = method.TypeArguments[index];
            try
            {
                var indent = new string(' ', depth * 2);
                var iterator = declaration.Body?.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
                    .OfType<YieldStatementSyntax>().Any() == true;
                var enumerable = iterator && method.ReturnType.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Generic.IEnumerable<T>" or "System.Collections.IEnumerable" or "System.Collections.Generic.IAsyncEnumerable<T>";
                _output.Append(indent).Append(method.IsAsync && !enumerable ? "async " : "")
                    .Append(iterator && !enumerable ? "function* " : "function ").Append(frame.Names[method]).Append('(')
                    .Append(string.Join(", ", declaration.ParameterList.Parameters.Select(ParameterDeclaration))).AppendLine(") {");
                EmitWithExpressionScope(depth + 1, () =>
                {
                    if (enumerable)
                    {
                        var parameters = string.Join(", ", declaration.ParameterList.Parameters.Select(ParameterName));
                        _output.Append(indent).Append("  return { [Symbol.").Append(method.IsAsync ? "asyncIterator" : "iterator")
                            .Append("]: (").Append(method.IsAsync ? "async " : "").Append("function* (").Append(parameters).AppendLine(") {");
                        EmitWithExpressionScope(depth + 2, () =>
                        {
                            EmitIteratorParameterCopies(declaration.ParameterList.Parameters, depth + 2);
                            EmitStatements(declaration.Body!.Statements, depth + 2);
                        });
                        _output.Append(indent).Append("  }).bind(this").Append(parameters.Length == 0 ? "" : ", " + parameters).AppendLine(") };");
                    }
                    else if (declaration.ExpressionBody is { } expression)
                        _output.Append(indent).Append("  return ").Append(ValueExpression(expression.Expression)).AppendLine(";");
                    else EmitStatements(declaration.Body!.Statements, depth + 1);
                });
                _output.Append(indent).AppendLine("}");
            }
            finally
            {
                _typeArguments = previousArguments;
                _declaredOutVariables = previousOut;
                _declaredPatternVariables = previousPatterns;
            }
        }
    }
}
