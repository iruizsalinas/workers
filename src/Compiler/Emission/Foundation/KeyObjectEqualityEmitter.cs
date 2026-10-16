using Microsoft.CodeAnalysis;

internal sealed partial class JavaScriptEmitter
{
    private bool _needsUserKeyDispatch;
    private readonly Dictionary<INamedTypeSymbol, (IMethodSymbol? Equality, IMethodSymbol? Hash)> _keyObjectDispatch = new(SymbolEqualityComparer.Default);

    private void RequireUserKeyDispatch()
    {
        _needsUserKeyDispatch = true;
        _helpers.Require(JavaScriptHelper.StructuralKeys);
    }

    private string UserKeyEquality(INamedTypeSymbol type, string left, string right, SyntaxNode source)
    {
        RequireUserKeyDispatch();
        var contract = type.AllInterfaces.FirstOrDefault(item => item.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>"
            && SymbolEqualityComparer.Default.Equals(item.TypeArguments[0], type));
        if (contract is not null && type.FindImplementationForInterfaceMember(contract.GetMembers("Equals").Single()) is IMethodSymbol method)
        {
            QueueUserType(type, source);
            return $"((left, right) => left == null || right == null ? left == right : left.{UserInstanceMethodName(method)}(right))({left}, {right})";
        }
        return $"{_helpers.Name("keyObjectEquals")}({left}, {right})";
    }

    private static IMethodSymbol? ObjectOverride(INamedTypeSymbol type, string name, int count)
    {
        for (var current = type; current is not null && IsUserInstanceType(current); current = current.BaseType)
            if (current.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(method => method.IsOverride && method.Parameters.Length == count
                && (count == 0 || method.Parameters[0].Type.SpecialType == SpecialType.System_Object)) is { } method) return method;
        return null;
    }

    private void PrepareUserKeyDispatch()
    {
        if (!_needsUserKeyDispatch) return;
        foreach (var type in _userTypes.Keys.ToArray().Where(type => !type.IsRecord))
        {
            if (_keyObjectDispatch.ContainsKey(type)) continue;
            var equals = ObjectOverride(type, "Equals", 1);
            var hash = ObjectOverride(type, "GetHashCode", 0);
            _keyObjectDispatch.Add(type, (equals, hash));
        }
    }

    private void EmitUserKeyDispatch()
    {
        foreach (var (type, methods) in _keyObjectDispatch)
        {
            _output.Append("Object.defineProperties(").Append(_userTypes[type]).AppendLine(".prototype, {");
            _output.Append("  [").Append(_helpers.Name("keyEqualsSlot")).Append("]: { value(other) { return ")
                .Append(methods.Equality is null ? "this === other" : "this." + UserInstanceMethodName(methods.Equality) + "(other)").AppendLine("; } },");
            _output.Append("  [").Append(_helpers.Name("keyHashSlot")).Append("]: { value() { return ")
                .Append(methods.Hash is null ? _helpers.Name("keyHash") + "(this)" : "this." + UserInstanceMethodName(methods.Hash) + "()").AppendLine("; } }");
            _output.AppendLine("});");
        }
    }

    private void ValidateKeyEquality(ITypeSymbol type, SyntaxNode source, HashSet<ITypeSymbol>? seen = null)
    {
        type = ResolveType(UnwrapNullable(type));
        seen ??= new(SymbolEqualityComparer.Default);
        if (!seen.Add(type)) return;
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Interface)
            throw new NotSupportedException($"WRK105: Composite key member type '{type}' requires runtime-erased equality; use a concrete key member type.");
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            foreach (var field in tuple.TupleElements) ValidateKeyEquality(field.Type, source, seen);
        else if (type is INamedTypeSymbol { IsRecord: true } record)
        {
            if (record.BaseType is { IsRecord: true } parent) ValidateKeyEquality(parent, source, seen);
            foreach (var member in RecordEqualityMembers(record)) ValidateKeyEquality(member.Type, source, seen);
        }
        else if (type is INamedTypeSymbol pair && pair.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            foreach (var argument in pair.TypeArguments) ValidateKeyEquality(argument, source, seen);
        else if (type.IsValueType && !SupportsLinqEquality(type) && !IsIndexType(type) && !IsRangeType(type))
            throw new NotSupportedException($"WRK105: Composite key member type '{type}' has unsupported value equality.");
    }
}
