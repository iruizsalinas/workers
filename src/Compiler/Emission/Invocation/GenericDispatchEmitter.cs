using Microsoft.CodeAnalysis;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<IMethodSymbol, string> _genericDispatchers = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, Dictionary<INamedTypeSymbol, IMethodSymbol>> _genericDispatchTargets = new(SymbolEqualityComparer.Default);

    private string GenericMethodTarget(IMethodSymbol method)
    {
        method = ResolveMethod(method);
        if (!method.IsVirtual && !method.IsOverride && !method.IsAbstract)
            return QueueUserMethod(method, method.DeclaringSyntaxReferences[0].GetSyntax());
        if (_genericDispatchers.TryGetValue(method, out var existing)) return existing;
        var name = _names.Get($"generic-dispatch:{method.ToDisplayString()}", $"dispatch${method.Name}");
        _genericDispatchers.Add(method, name);
        _genericDispatchTargets.Add(method, new(SymbolEqualityComparer.Default));
        return name;
    }

    // Discover dispatch targets after reachable types are known. Emitting a target
    // can discover further types, so module emission repeats this to a fixed point.
    private void PrepareGenericDispatch()
    {
        foreach (var method in _genericDispatchers.Keys.ToArray())
            foreach (var type in _userTypes.Keys.ToArray())
            {
                if (type.IsAbstract || _genericDispatchTargets[method].ContainsKey(type)) continue;
                var conversion = _compilation.ClassifyConversion(type, method.ContainingType);
                if (!conversion.IsIdentity && !conversion.IsImplicit) continue;
                IMethodSymbol? target;
                if (IsSourceInterface(method.ContainingType))
                {
                    target = FindInterfaceImplementation(type, method.ConstructedFrom) as IMethodSymbol;
                    if (target is not null && !IsSourceInterface(target.ContainingType)
                        && (target.IsVirtual || target.IsOverride || target.IsAbstract))
                        target = FindVirtualImplementation(type, target);
                }
                else
                    target = FindVirtualImplementation(type, method);
                if (target is null || target.IsAbstract) continue;
                target = target.ConstructedFrom.Construct(method.TypeArguments.ToArray());
                _genericDispatchTargets[method].Add(type, target);
                QueueUserMethod(target, target.DeclaringSyntaxReferences[0].GetSyntax());
            }
    }

    private static IMethodSymbol? FindVirtualImplementation(INamedTypeSymbol type, IMethodSymbol slot)
    {
        // Constructed generic methods do not expose OverriddenMethod in Roslyn.
        // Find the virtual slot on the unconstructed method, then specialize its implementation.
        slot = slot.ConstructedFrom;
        while (slot.OverriddenMethod is { } parent) slot = parent;
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var method in current.GetMembers(slot.Name).OfType<IMethodSymbol>())
            {
                var root = method;
                while (root.OverriddenMethod is { } parent) root = parent;
                if (SymbolEqualityComparer.Default.Equals(root.OriginalDefinition, slot.OriginalDefinition)) return method;
            }
        return null;
    }

    private void EmitGenericDispatchers()
    {
        foreach (var (method, name) in _genericDispatchers)
        {
            var parameters = method.Parameters.Select((_, index) => $"$arg{index}").ToArray();
            _output.Append("function ").Append(name).Append('(').Append(string.Join(", ", parameters)).AppendLine(") {");
            _output.AppendLine($"  if (this == null) throw {NullReferenceException()};");
            _output.AppendLine("  switch (Object.getPrototypeOf(this)) {");
            foreach (var (type, target) in _genericDispatchTargets[method])
                _output.Append("    case ").Append(_userTypes[type]).Append(".prototype: return ")
                    .Append(_userMethods[target]).Append(".call(this")
                    .Append(parameters.Length == 0 ? "" : ", " + string.Join(", ", parameters)).AppendLine(");");
            _output.AppendLine("    default: throw new TypeError(\"Unknown C# generic method receiver.\");");
            _output.AppendLine("  }");
            _output.AppendLine("}");
        }
    }
}
