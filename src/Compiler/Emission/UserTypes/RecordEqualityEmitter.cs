using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Records compare by value: the synthesized Equals compares every instance field with
// EqualityComparer<T>.Default. Generated per record type because plain records are object literals.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _recordEqualityFunctions = new(SymbolEqualityComparer.Default);
    private readonly Queue<(INamedTypeSymbol Type, SyntaxNode Source)> _pendingRecordEqualityFunctions = new();

    private static bool IsSynthesizedRecordMember(IMethodSymbol? method) =>
        method is { IsImplicitlyDeclared: true, ContainingType.IsRecord: true }
        && IsUserInstanceType(method.ContainingType);

    private string RecordEquals(INamedTypeSymbol type, string left, string right, SyntaxNode source, bool referenceComparison = false)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (UsesUserHierarchy(type))
        {
            QueueUserType(type, source);
            var compare = $"left == null ? right == null : left.{RecordEqualitySlot}(right)";
            return $"((left, right) => {(referenceComparison ? "left === right || " : "") }({compare}))({left}, {right})";
        }
        var helper = RecordEqualityFunction(type, source);
        // The synthesized == operator short-circuits identical references. A custom typed
        // Equals invoked by EqualityComparer must still run, including for the same instance.
        return referenceComparison && UserRecordEqualsMethod(type) is not null
            ? $"((left, right) => left === right || {helper}(left, right))({left}, {right})"
            : $"{helper}({left}, {right})";
    }

    private static IMethodSymbol? UserRecordEqualsMethod(INamedTypeSymbol type) =>
        type.GetMembers("Equals").OfType<IMethodSymbol>().SingleOrDefault(method =>
            !method.IsImplicitlyDeclared && !method.IsStatic && method.Parameters is [{ } parameter]
            && SymbolEqualityComparer.Default.Equals(parameter.Type.OriginalDefinition, type.OriginalDefinition));

    private string RecordEqualityFunction(INamedTypeSymbol type, SyntaxNode source)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (_recordEqualityFunctions.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"record-equals:{type.ToDisplayString()}", $"recordEquals${type.Name}");
        _recordEqualityFunctions.Add(type, name);
        _pendingRecordEqualityFunctions.Enqueue((type, source));
        return name;
    }

    private bool EmitPendingRecordEqualityFunctions()
    {
        var emitted = false;
        while (_pendingRecordEqualityFunctions.TryDequeue(out var pending))
        {
            emitted = true;
            var (type, source) = pending;
            using var specialization = SpecializationScope(type);
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var customEquals = UserRecordEqualsMethod(type);
            if (customEquals is not null)
            {
                QueueUserType(type, source);
                _output.Append("function ").Append(_recordEqualityFunctions[type]).AppendLine("(left, right) {");
                _output.AppendLine("  if (left == null) return right == null;");
                _output.Append("  return ").Append(QueueUserMethod(customEquals, source)).AppendLine(".call(left, right);");
                _output.AppendLine("}").AppendLine();
                continue;
            }
            var comparisons = RecordEqualityMembers(type)
                .Select(member => ValueEquality(member.Type,
                    member.Symbol is IPropertySymbol property ? UserPropertyStorage("left", property) : UserMemberAccess("left", member.Symbol),
                    member.Symbol is IPropertySymbol otherProperty ? UserPropertyStorage("right", otherProperty) : UserMemberAccess("right", member.Symbol), source))
                .ToArray();
            if (type.BaseType is { IsRecord: true } parent)
                comparisons = [$"{RecordEqualityFunction(parent, source)}(left, right)", .. comparisons];
            _output.Append("function ").Append(_recordEqualityFunctions[type]).AppendLine("(left, right) {");
            if (!type.IsValueType) _output.AppendLine("  if (left === right) return true;");
            _output.AppendLine("  if (left == null || right == null) return left == right;");
            if (RequiresUserClass(type))
                _output.AppendLine("  if (Object.getPrototypeOf(left) !== Object.getPrototypeOf(right)) return false;");
            _output.Append("  return ").Append(comparisons.Length == 0 ? "true" : string.Join("\n    && ", comparisons)).AppendLine(";");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }

    // Instance state: positional and declared auto-properties plus explicit fields.
    private static IEnumerable<(ISymbol Symbol, ITypeSymbol Type)> RecordEqualityMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsStatic) continue;
            if (member is IFieldSymbol { AssociatedSymbol: IPropertySymbol } backing && IsFieldKeywordBackingField(backing))
                yield return (backing, backing.Type);
            else if (member is IFieldSymbol { AssociatedSymbol: IPropertySymbol property })
                yield return (property, property.Type);
            else if (member is IEventSymbol ev && ev.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is VariableDeclaratorSyntax))
                yield return (ev, ev.Type);
            else if (member is IFieldSymbol { IsImplicitlyDeclared: false } field)
                yield return (field, field.Type);
        }
    }

    private string ValueEquality(ITypeSymbol type, string left, string right, SyntaxNode source)
    {
        type = ResolveType(type);
        if (type is INamedTypeSymbol { IsAnonymousType: true } anonymous)
        {
            var comparisons = anonymous.GetMembers().OfType<IPropertySymbol>().Select(property => {
                var key = System.Text.Json.JsonSerializer.Serialize(AnonymousPropertyName(property));
                return ValueEquality(property.Type, $"a[{key}]", $"b[{key}]", source);
            }).ToArray();
            return $"((a, b) => a == null || b == null ? a == b : {(comparisons.Length == 0 ? "true" : string.Join(" && ", comparisons))})({left}, {right})";
        }
        if (type.TypeKind == TypeKind.Delegate) return $"{DelegateHelper("delegateEquals")}({left}, {right})";
        if (IsIndexType(type)) return $"((a, b) => a[0] === b[0] && a[1] === b[1])({left}, {right})";
        if (IsRangeType(type)) return $"((a, b) => a[0][0] === b[0][0] && a[0][1] === b[0][1] && a[1][0] === b[1][0] && a[1][1] === b[1][1])({left}, {right})";
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            return $"((a, b) => {string.Join(" && ", tuple.TupleElements.Select((field, index) => ValueEquality(field.Type, $"a[{index}]", $"b[{index}]", source)))})({left}, {right})";
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return $"((a, b) => a == null || b == null ? a == b : {ValueEquality(nullable.TypeArguments[0], "a", "b", source)})({left}, {right})";
        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            return $"((a, b) => a === b || a !== a && b !== b)({left}, {right})";
        if (type is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
            return $"((left, right) => left == null || right == null ? left == right : {RecordEquals(record, "left", "right", source)})({left}, {right})";
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
            return UserKeyEquality(user, left, right, source);
        if (type is INamedTypeSymbol pair
            && pair.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return $"((a, b) => {ValueEquality(pair.TypeArguments[0], "a[0]", "b[0]", source)} " +
                $"&& {ValueEquality(pair.TypeArguments[1], "a[1]", "b[1]", source)})({left}, {right})";
        return $"({left} === {right})";
    }
}
