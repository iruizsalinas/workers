using Microsoft.CodeAnalysis;

// Specialize emitted code while retaining Roslyn's original bound operations.
// In particular, substituting syntax and recompiling would change overload binding.
internal sealed partial class JavaScriptEmitter
{
    private Dictionary<ITypeParameterSymbol, ITypeSymbol> _typeArguments = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, int> _specializationCounts = new(SymbolEqualityComparer.Default);

    private void CountSpecialization(ISymbol symbol, SyntaxNode source)
    {
        // Polymorphic recursion can otherwise generate an unbounded number of
        // closed methods/readers from a finite source program.
        var definition = symbol.OriginalDefinition;
        var count = _specializationCounts.GetValueOrDefault(definition) + 1;
        if (count > 128)
            throw WorkerDiagnostics.WithLocation(new NotSupportedException(
                $"WRK119: '{definition}' exceeds the limit of 128 emitted specializations. Check for expanding generic recursion."), source);
        _specializationCounts[definition] = count;
    }

    private ITypeSymbol ResolveType(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol parameter)
            return _typeArguments.TryGetValue(parameter, out var argument) && !SymbolEqualityComparer.Default.Equals(parameter, argument)
                ? ResolveType(argument) : type;
        if (type is IArrayTypeSymbol array)
            return _compilation.CreateArrayTypeSymbol(ResolveType(array.ElementType), array.Rank);
        if (type is not INamedTypeSymbol named || !named.IsGenericType) return type;
        var definition = named.OriginalDefinition;
        if (named.ContainingType is { } containing)
        {
            var owner = (INamedTypeSymbol)ResolveType(containing);
            definition = owner.GetTypeMembers(named.Name, named.Arity).Single();
        }
        return named.Arity == 0 ? definition : definition.Construct(named.TypeArguments.Select(ResolveType).ToArray());
    }

    private IMethodSymbol ResolveMethod(IMethodSymbol method)
    {
        var owner = (INamedTypeSymbol)ResolveType(method.ContainingType);
        var bound = SymbolEqualityComparer.Default.Equals(owner, method.ContainingType) ? method
            : owner.GetMembers(method.Name).OfType<IMethodSymbol>().First(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));
        return method.IsGenericMethod
            ? bound.ConstructedFrom.Construct(method.TypeArguments.Select(ResolveType).ToArray()) : bound;
    }

    private IDisposable SpecializationScope(INamedTypeSymbol type, IMethodSymbol? method = null)
    {
        var previous = _typeArguments;
        var previousOutVariables = _declaredOutVariables;
        var previousPatternVariables = _declaredPatternVariables;
        _declaredOutVariables = new(SymbolEqualityComparer.Default);
        _declaredPatternVariables = new(SymbolEqualityComparer.Default);
        _typeArguments = new(SymbolEqualityComparer.Default);
        for (var owner = type; owner is not null; owner = owner.ContainingType)
            for (var index = 0; index < owner.Arity; index++)
                _typeArguments[owner.OriginalDefinition.TypeParameters[index]] = owner.TypeArguments[index];
        for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
            for (var index = 0; index < parent.Arity; index++)
                _typeArguments[parent.OriginalDefinition.TypeParameters[index]] = parent.TypeArguments[index];
        if (method is not null)
            for (var index = 0; index < method.Arity; index++)
                _typeArguments[method.OriginalDefinition.TypeParameters[index]] = method.TypeArguments[index];
        return new RestoreSpecialization(() =>
        {
            _typeArguments = previous;
            _declaredOutVariables = previousOutVariables;
            _declaredPatternVariables = previousPatternVariables;
        });
    }

    private sealed class RestoreSpecialization(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
