using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private bool _usesObjectText;
    private bool _needsDynamicObjectText;
    private bool _usesBoxedEnumTypes;
    private string BoxedEnumTypeSlot
    {
        get
        {
            _usesObjectText = true;
            _usesBoxedEnumTypes = true;
            return _names.Get("boxed-enum-type-symbol", "boxedEnumType");
        }
    }
    private string ObjectTextSlot => _names.Get("object-text-symbol", "objectTextSlot");
    private string ObjectTextFunction => _names.Get("object-text-helper", "objectText");
    private static bool IsWebValueBoundary(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property }
                && property.Property.DeclaringSyntaxReferences.Length == 0
                && property.Property.ContainingNamespace.ToDisplayString() is "Workers" or "Workers.Bindings") return true;
            if (current is IArgumentOperation { Parameter.ContainingSymbol: IMethodSymbol method })
                return method.DeclaringSyntaxReferences.Length == 0
                    && method.ContainingNamespace.ToDisplayString().StartsWith("Workers", StringComparison.Ordinal);
        }
        return false;
    }
    private readonly Dictionary<string, (SyntaxNode Source, IReadOnlyList<(string Name, string Storage, ITypeSymbol Type)> Members)> _anonymousTextFunctions = new();
    private readonly Dictionary<INamedTypeSymbol, string> _anonymousTextNames = new(SymbolEqualityComparer.Default);
    private readonly Queue<string> _pendingAnonymousTextFunctions = new();
    private readonly HashSet<string> _requiredAnonymousTextFunctions = new();
    private readonly HashSet<string> _boxedAnonymousTextFunctions = new();
    private readonly Dictionary<INamedTypeSymbol, SyntaxNode> _boxedRecordTextTypes = new(SymbolEqualityComparer.Default);

    private string RegisterAnonymousText(INamedTypeSymbol type, IReadOnlyList<(string Name, string Storage, ITypeSymbol Type)> members, SyntaxNode source)
    {
        var name = _names.Get($"anonymous-text:{source.SyntaxTree.FilePath}:{source.SpanStart}:{string.Join(",", members.Select(member => member.Type.ToDisplayString()))}", "anonymousText");
        _anonymousTextFunctions.TryAdd(name, (source, members));
        _anonymousTextNames[type] = name;
        return name;
    }

    private string AnonymousText(INamedTypeSymbol type, string value, SyntaxNode source, bool require = true)
    {
        if (!_anonymousTextNames.TryGetValue(type, out var name)) throw Unsupported("WRK108", source);
        if (require && _requiredAnonymousTextFunctions.Add(name)) _pendingAnonymousTextFunctions.Enqueue(name);
        if (!require) _boxedAnonymousTextFunctions.Add(name);
        return $"{name}({value})";
    }

    private string ObjectText(string value)
    {
        _usesObjectText = true;
        _needsDynamicObjectText = true;
        return $"{ObjectTextFunction}({value})";
    }

    private string UserObjectText(INamedTypeSymbol type, string value, SyntaxNode source)
    {
        QueueUserType(type, source);
        _usesObjectText = true;
        return $"{value}[{ObjectTextSlot}]()";
    }

    private string UserObjectTextBody(INamedTypeSymbol type, string value)
    {
        for (var current = type; current is not null && IsUserInstanceType(current); current = current.BaseType)
            if (current.GetMembers("ToString").OfType<IMethodSymbol>().FirstOrDefault(method =>
                    !method.IsStatic && method.Parameters.Length == 0) is { } method)
                return $"{value}.{UserInstanceMethodName(method)}()";
        return JsonSerializer.Serialize(RuntimeTypeName(type));
    }

    private static string RuntimeTypeName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return RuntimeTypeName(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is not INamedTypeSymbol named) return type.ToDisplayString();
        if (named.IsUnboundGenericType) named = named.OriginalDefinition;
        var owners = new Stack<INamedTypeSymbol>();
        for (var current = named; current is not null; current = current.ContainingType) owners.Push(current);
        var arguments = owners.SelectMany(owner => owner.TypeArguments).ToArray();
        var prefix = named.ContainingNamespace.IsGlobalNamespace ? "" : named.ContainingNamespace.ToDisplayString() + ".";
        return prefix + string.Join("+", owners.Select(owner => owner.MetadataName))
            + (arguments.Length == 0 ? "" : "[" + string.Join(",", arguments.Select(RuntimeTypeName)) + "]");
    }

    private static bool HasCollectionTypeName(ITypeSymbol type) => type is INamedTypeSymbol named
        && named.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal);

    private string BoxEnumText(ITypeSymbol type, string value)
    {
        _usesObjectText = true;
        var identity = JsonSerializer.Serialize(RuntimeTypeName(ResolveType(type)));
        return $"((value) => Object.defineProperty(Object.defineProperty(Object(value), {BoxedEnumTypeSlot}, {{ value: {identity} }}), {ObjectTextSlot}, {{ value: function() {{ return {EnumText(type, "value")}; }} }}))({value})";
    }

    private string AttachObjectText(ITypeSymbol type, string value, SyntaxNode source)
    {
        _usesObjectText = true;
        var text = type is INamedTypeSymbol { IsRecord: true } record
            ? BoxedRecordText(record, source)
            : type is INamedTypeSymbol { IsTupleType: true } tuple ? TupleText(tuple, "this", source)
            : type is INamedTypeSymbol pair && IsKeyValuePair(pair) ? KeyValuePairText(pair, "this", source)
            : type is INamedTypeSymbol { IsAnonymousType: true } anonymous ? AnonymousText(anonymous, "this", source, require: false)
            : JsonSerializer.Serialize(RuntimeTypeName(type));
        return $"((value) => value == null ? value : Object.defineProperty(value, {ObjectTextSlot}, {{ configurable: true, value: function() {{ return {text}; }} }}))({value})";
    }

    private string BoxedRecordText(INamedTypeSymbol type, SyntaxNode source)
    {
        _boxedRecordTextTypes.TryAdd(type, source);
        if (RecordToStringMethod(type) is { IsImplicitlyDeclared: false } customText)
            return $"this.{UserInstanceMethodName(customText)}()";
        return UsesUserHierarchy(type) ? $"this.{RecordTextSlot}()" : $"{RecordTextFunction(type, source, require: false)}(this)";
    }

    private string EmitObjectTextSupport() => !_usesObjectText ? "" : $$"""
        const {{ObjectTextSlot}} = Symbol();
        {{(_usesBoxedEnumTypes ? $"const {BoxedEnumTypeSlot} = Symbol();" : "")}}
        function {{ObjectTextFunction}}(value) {
          if (value == null) return "";
          if (typeof value === "boolean") return value ? "True" : "False";
          if (typeof value !== "object") return String(value);
          if (typeof value[{{ObjectTextSlot}}] === "function") return value[{{ObjectTextSlot}}]() ?? "";
          return "System.Object";
        }

        """;

    private void EmitUserObjectTextMember(INamedTypeSymbol type, SyntaxNode source)
    {
        _usesObjectText = true;
        var body = type.IsRecord && RecordToStringMethod(type) is { IsImplicitlyDeclared: false } customText
            ? $"this.{UserInstanceMethodName(customText)}()"
            : type.IsRecord
            ? UsesUserHierarchy(type) ? $"this.{RecordTextSlot}()" : $"{RecordTextFunction(type, source, require: false)}(this)"
            : UserObjectTextBody(type, "this");
        _output.Append("  [").Append(ObjectTextSlot).Append("]() { return ").Append(body).AppendLine("; }");
    }

    private void PrepareObjectTextDispatch()
    {
        if (_needsDynamicObjectText)
        {
            foreach (var (type, source) in _boxedRecordTextTypes.ToArray()) RecordText(type, "value", source);
            foreach (var name in _boxedAnonymousTextFunctions)
                if (_requiredAnonymousTextFunctions.Add(name)) _pendingAnonymousTextFunctions.Enqueue(name);
        }
        while (_pendingAnonymousTextFunctions.TryDequeue(out var name))
        {
            var entry = _anonymousTextFunctions[name];
            var members = entry.Members.Select(member => JsonSerializer.Serialize(member.Name + " = ") + " + "
                + RecordMemberText(member.Type, $"value[Object.hasOwn(value, {JsonSerializer.Serialize(member.Name)}) ? {JsonSerializer.Serialize(member.Name)} : {JsonSerializer.Serialize(LowerFirst(member.Name))}]", entry.Source)).ToArray();
            var text = members.Length == 0 ? "\"{ }\"" : "\"{ \" + " + string.Join(" + \", \" + ", members) + " + \" }\"";
            _output.Append("function ").Append(name).Append("(value) { return ").Append(text).AppendLine("; }");
        }
    }
}
