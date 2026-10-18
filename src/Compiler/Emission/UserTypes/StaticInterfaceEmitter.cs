using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<IMethodSymbol, string> _sourceAccessors = new(SymbolEqualityComparer.Default);
    private readonly Queue<IMethodSymbol> _pendingSourceAccessors = new();

    private ISymbol StaticInterfaceImplementation(ISymbol member, ITypeSymbol? constrained, SyntaxNode source)
    {
        if (!member.IsStatic || member.ContainingType.TypeKind != TypeKind.Interface) return member;
        if (!member.IsAbstract && !member.IsVirtual) return member;
        var type = constrained is null ? null : ResolveType(constrained) as INamedTypeSymbol;
        if (type is null) throw UnsupportedSymbol(member, source);
        var contract = (INamedTypeSymbol)ResolveType(member.ContainingType);
        var bound = contract.GetMembers().FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, member.OriginalDefinition)) ?? member;
        var implementation = type.FindImplementationForInterfaceMember(bound);
        if (implementation is null && !member.IsAbstract) implementation = bound;
        if (implementation is null || implementation.IsAbstract) throw UnsupportedSymbol(member, source);
        if (member is IMethodSymbol { IsGenericMethod: true } method && implementation is IMethodSymbol target)
            return target.ConstructedFrom.Construct(method.TypeArguments.Select(ResolveType).ToArray());
        return implementation;
    }

    private ITypeSymbol? StaticMemberConstraint(SyntaxNode source) => source is MemberAccessExpressionSyntax access
        ? _model.GetTypeInfo(access.Expression).Type : null;

    private string QueueSourceAccessor(IMethodSymbol method, SyntaxNode source)
    {
        method = ResolveMethod(method);
        if (method.IsAbstract || method.IsExtern || method.ReturnsByRef || method.ReturnsByRefReadonly
            || method.Parameters.Any(parameter => parameter.RefKind is RefKind.Ref or RefKind.In)
            || (method.AssociatedSymbol ?? method).DeclaringSyntaxReferences.Length != 1) throw UnsupportedSymbol(method, source);
        if (_sourceAccessors.TryGetValue(method, out var existing)) return existing;
        CountSpecialization(method, source);
        var name = _names.Get($"source-accessor:{method.ToDisplayString()}", $"cs${method.ContainingType.Name}${method.Name}");
        _sourceAccessors.Add(method, name);
        _pendingSourceAccessors.Enqueue(method);
        return name;
    }

    private void EmitSourceAccessors()
    {
        while (_pendingSourceAccessors.TryDequeue(out var method))
        {
            using var scope = SpecializationScope(method.ContainingType, method);
            var syntax = method.DeclaringSyntaxReferences.Length == 1 ? method.DeclaringSyntaxReferences[0].GetSyntax()
                : (method.AssociatedSymbol ?? method).DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(syntax.SyntaxTree);
            _diagnosticNode = syntax;
            if (method.MethodKind is MethodKind.EventAdd or MethodKind.EventRemove)
            {
                _output.Append("function ").Append(_sourceAccessors[method]).Append('(')
                    .Append(UserIdentifier(method.Parameters[0], method.Parameters[0].Name)).AppendLine(") {");
                EmitExplicitStaticInitialization(method.ContainingType, 1);
                EmitEventAccessorBody(method, syntax, 1);
                _output.AppendLine("}");
                continue;
            }
            var (expression, body) = syntax switch
            {
                AccessorDeclarationSyntax accessor => (accessor.ExpressionBody?.Expression, accessor.Body),
                ArrowExpressionClauseSyntax arrow => (arrow.Expression, null),
                PropertyDeclarationSyntax property => (property.ExpressionBody?.Expression, null),
                IndexerDeclarationSyntax indexer => (indexer.ExpressionBody?.Expression, null),
                OperatorDeclarationSyntax op => (op.ExpressionBody?.Expression, op.Body),
                ConversionOperatorDeclarationSyntax op => (op.ExpressionBody?.Expression, op.Body),
                _ => throw UnsupportedSymbol(method, syntax)
            };
            _output.Append("function ").Append(_sourceAccessors[method]).Append('(')
                .Append(string.Join(", ", method.Parameters.Select(parameter => UserIdentifier(parameter, parameter.Name)))).AppendLine(") {");
            EmitWithExpressionScope(1, () =>
            {
                EmitExplicitStaticInitialization(method.ContainingType, 1);
                if (method.MethodKind == MethodKind.PropertySet) EmitSetterValueCopy(method, 1);
                if (expression is not null) _output.Append("  ").Append(method.ReturnsVoid ? "" : "return ").Append(ValueExpression(expression)).AppendLine(";");
                else EmitStatements(body?.Statements ?? [], 1);
            });
            _output.AppendLine("}");
        }
    }

    private string SourceOperator(IMethodSymbol method, ITypeSymbol? constrained, SyntaxNode source, params IOperation[] operands)
    {
        method = (IMethodSymbol)StaticInterfaceImplementation(method, constrained, source);
        return $"{QueueSourceAccessor(method, source)}({string.Join(", ", operands.Select(ValueExpression))})";
    }

    private string SourceOperatorCall(IMethodSymbol method, ITypeSymbol? constrained, SyntaxNode source, params string[] operands)
    {
        method = (IMethodSymbol)StaticInterfaceImplementation(method, constrained, source);
        return $"{QueueSourceAccessor(method, source)}({string.Join(", ", operands)})";
    }
}
