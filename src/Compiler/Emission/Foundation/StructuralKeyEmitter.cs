using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _recordHashes = new(SymbolEqualityComparer.Default);
    private readonly Queue<INamedTypeSymbol> _pendingRecordHashes = new();
    private readonly HashSet<INamedTypeSymbol> _hashDispatchTypes = new(SymbolEqualityComparer.Default);
    private bool _needsRecordHashDispatch;
    private string RecordHashSlot => _names.Get("record-hash-slot", "hashRecord");

    private bool IsCompositeKey(ITypeSymbol type) => ResolveType(UnwrapNullable(type)) is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { IsRecord: true };
    private bool UsesKeyDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named && IsDictionary(type)
        && ResolveType(DictionaryKeyType(named)).SpecialType != SpecialType.System_String;
    private static ITypeSymbol DictionaryKeyType(INamedTypeSymbol type) =>
        type.TypeArguments.Length == 2 ? type.TypeArguments[0] : type.AllInterfaces.First(item => IsDictionary(item)).TypeArguments[0];

    private string KeyHash(ITypeSymbol type, string value, SyntaxNode source)
    {
        _helpers.Require(JavaScriptHelper.StructuralKeys);
        type = ResolveType(type);
        if (type.TypeKind == TypeKind.Delegate)
        {
            DelegateHelper("delegateEntries");
            return $"{_helpers.Name("keyHashCombine")}({_helpers.Name("delegateEntries")}({value}).map(entry => {_helpers.Name("keyHashCombine")}([{_helpers.Name("keyHash")}(entry.target), {_helpers.Name("keyHash")}(entry.method)])))";
        }
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return $"((value) => value == null ? 0 : {KeyHash(nullable.TypeArguments[0], "value", source)})({value})";
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            return $"((value) => {_helpers.Name("keyHashCombine")}([{string.Join(", ", tuple.TupleElements.Select((field, i) => KeyHash(field.Type, $"value[{i}]", source)))}]))({value})";
        if (type is INamedTypeSymbol pair && pair.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return $"((value) => {_helpers.Name("keyHashCombine")}([{KeyHash(pair.TypeArguments[0], "value[0]", source)}, {KeyHash(pair.TypeArguments[1], "value[1]", source)}]))({value})";
        if (IsIndexType(type))
            return $"((value) => {_helpers.Name("keyHashCombine")}([{_helpers.Name("keyHash")}(value[0]), {_helpers.Name("keyHash")}(value[1])]))({value})";
        if (IsRangeType(type))
        {
            var indexType = _compilation.GetTypeByMetadataName("System.Index")!;
            return $"((value) => {_helpers.Name("keyHashCombine")}([{KeyHash(indexType, "value[0]", source)}, {KeyHash(indexType, "value[1]", source)}]))({value})";
        }
        if (type is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record))
        {
            if (UsesUserHierarchy(record)) _needsRecordHashDispatch = true;
            return UsesUserHierarchy(record)
                ? $"((value) => value == null ? 0 : value.{RecordHashSlot}())({value})"
                : $"{RecordHashFunction(record)}({value})";
        }
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
        {
            RequireUserKeyDispatch();
            return $"{_helpers.Name("keyObjectHash")}({value})";
        }
        return $"{_helpers.Name("keyHash")}({value})";
    }

    private void PrepareRecordHashDispatch()
    {
        if (!_needsRecordHashDispatch) return;
        foreach (var type in _userTypes.Keys.ToArray().Where(type => type.IsRecord))
            if (_hashDispatchTypes.Add(type)) RecordHashFunction(type);
    }

    private void EmitRecordHashDispatch()
    {
        foreach (var type in _hashDispatchTypes)
            _output.Append(_userTypes[type]).Append(".prototype.").Append(RecordHashSlot)
                .Append(" = function() { return ").Append(_recordHashes[type]).AppendLine("(this); };");
    }

    private string RecordHashFunction(INamedTypeSymbol type)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (_recordHashes.TryGetValue(type, out var name)) return name;
        name = _names.Get($"record-hash:{type}", $"hash${type.Name}");
        _recordHashes.Add(type, name); _pendingRecordHashes.Enqueue(type);
        return name;
    }

    private void EmitRecordHashes()
    {
        while (_pendingRecordHashes.TryDequeue(out var type))
        {
            using var scope = SpecializationScope(type);
            var source = type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(source.SyntaxTree);
            _helpers.Require(JavaScriptHelper.StructuralKeys);
            var custom = type.GetMembers("GetHashCode").OfType<IMethodSymbol>().SingleOrDefault(item => !item.IsImplicitlyDeclared && !item.IsStatic && item.Parameters.Length == 0);
            var parts = RecordEqualityMembers(type).Select(member => KeyHash(member.Type,
                member.Symbol is IPropertySymbol property ? UserPropertyStorage("value", property) : UserMemberAccess("value", member.Symbol), source)).ToList();
            if (type.BaseType is { IsRecord: true } parent) parts.Insert(0, $"{RecordHashFunction(parent)}(value)");
            var body = custom is not null ? $"{QueueUserMethod(custom, source)}.call(value)"
                : $"{_helpers.Name("keyHashCombine")}([{string.Join(", ", parts)}])";
            _output.Append("function ").Append(_recordHashes[type]).Append("(value) { return value == null ? 0 : ").Append(body).AppendLine("; }");
        }
    }

    private string KeyCollection(INamedTypeSymbol type, SyntaxNode source, string items = "[]")
    {
        var set = IsSet(type);
        var key = ResolveType(set ? type.TypeArguments[0] : DictionaryKeyType(type));
        if (!SupportsLinqEquality(key) && !IsCompositeKey(key)) throw UnsupportedSymbol(type, source);
        ValidateKeyEquality(key, source);
        var value = set ? key : ResolveType(type.TypeArguments[1]);
        var keyText = IsCompositeKey(key) ? "null" : key.TypeKind == TypeKind.Enum ? $"key => {EnumText(key, "key")}" : key.SpecialType == SpecialType.System_Boolean ? "key => key ? 'True' : 'False'" : "key => String(key)";
        return $"{_helpers.Require(JavaScriptHelper.StructuralKeys)}({EqualityFunction(key, source)}, value => {KeyHash(key, "value", source)}, "
            + $"value => {CopyValue(key, "value")}, value => {CopyValue(value, "value")}, {set.ToString().ToLowerInvariant()}, {items}, {keyText})";
    }

    private string? KeyCollectionInvocation(SyntaxNode source, INamedTypeSymbol type, string receiver, string name, string[] arguments)
    {
        if (!UsesKeyDictionary(type) && !(IsSet(type) && IsCompositeKey(type.TypeArguments[0]))) return null;
        var set = IsSet(type);
        return (name, arguments.Length) switch
        {
            ("Add", 1) when set => $"{receiver}.add({arguments[0]})",
            ("Add", 2) when !set => $"{receiver}.add({string.Join(", ", arguments)})",
            ("TryAdd", 2) when !set => $"{receiver}.tryAdd({string.Join(", ", arguments)})",
            ("Contains" or "ContainsKey", 1) => $"{receiver}.has({arguments[0]})",
            ("Remove", 1) => $"{receiver}.delete({arguments[0]})",
            ("Clear", 0) => $"{receiver}.clear()",
            ("GetValueOrDefault", 1 or 2) when !set => $"{receiver}.getOrDefault({arguments[0]}, {(arguments.Length == 2 ? arguments[1] : DefaultValueText(type.TypeArguments[1], source))})",
            ("UnionWith" or "IntersectWith" or "ExceptWith" or "SymmetricExceptWith" or "IsSubsetOf" or "IsSupersetOf" or "IsProperSubsetOf" or "IsProperSupersetOf" or "Overlaps" or "SetEquals", 1) when set =>
                $"{RequireHelperName(JavaScriptHelper.StructuralKeys, "keySetOperation")}({receiver}, {KeyCollection(type, source, arguments[0])}, {System.Text.Json.JsonSerializer.Serialize(name)})",
            _ => null
        };
    }

    private string CreateKeyCollection(INamedTypeSymbol type, BaseObjectCreationExpressionSyntax syntax, IMethodSymbol constructor, ArgumentSyntax[] arguments)
    {
        var items = "[]";
        if (arguments.Length == 1 && (IsDictionary(constructor.Parameters[0].Type)
            || constructor.Parameters[0].Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            items = Expression(arguments[0].Expression);
        else if (arguments.Length != 0) throw UnsupportedSymbol(constructor, syntax);
        var creation = KeyCollection(type, syntax, items);
        if (syntax.Initializer is null) return creation;
        var collection = _names.Get($"key-initializer:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}", "collection");
        var calls = syntax.Initializer.Expressions.Select(item => item switch
        {
            AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax index } assignment =>
                $"{collection}.set({Expression(index.ArgumentList.Arguments.Single().Expression)}, {Expression(assignment.Right)});",
            InitializerExpressionSyntax pair when pair.Expressions.Count == 2 =>
                $"{collection}.add({Expression(pair.Expressions[0])}, {Expression(pair.Expressions[1])});",
            _ when IsSet(type) => $"{collection}.add({Expression(item)});",
            _ => throw Unsupported("WRK101", item)
        });
        var asynchronous = _model.GetOperation(syntax.Initializer) is { } initializer && ContainsAwait(initializer);
        var body = $"({(asynchronous ? "async " : "")}({collection}) => {{ {string.Join(" ", calls)} return {collection}; }})({creation})";
        return asynchronous ? $"(await {body})" : body;
    }
}
