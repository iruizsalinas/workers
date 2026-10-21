using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// Typed reads hydrate C# classes and records without emulating a .NET serializer.
internal sealed partial class JavaScriptEmitter
{
    private const int JsonWebMode = 0;
    private const int JsonRowMode = 1;
    private const int JsonQueryMode = 2;

    private void RegisterJsonValueType(ITypeSymbol type, SyntaxNode source, int mode)
    {
        type = UnwrapNullable(ResolveType(type));
        if (type is INamedTypeSymbol contract && IsSourceInterface(contract) || type.IsAbstract && type.TypeKind == TypeKind.Class && type.DeclaringSyntaxReferences.Length != 0)
            throw new NotSupportedException($"WRK119: Typed JSON reads need a concrete type, not '{type}'. Deserialize a concrete user type before assigning to its base type or interface.");
        if (JsonElementType(type) is { } element)
        {
            RegisterJsonValueType(element, source, mode);
            return;
        }
        if (type is not INamedTypeSymbol user || !IsUserInstanceType(user) || IsException(user))
        {
            var typeNamespace = type.ContainingNamespace.ToDisplayString();
            if (type.SpecialType == SpecialType.None && type.TypeKind != TypeKind.Enum
                && (typeNamespace == "System" || typeNamespace.StartsWith("System.", StringComparison.Ordinal)))
                throw new NotSupportedException($"WRK119: Typed JSON reads do not support .NET member type '{type}'. Use Workers runtime values.");
            return;
        }
        if (!_jsonReaders.Add((user, mode))) return;
        if (user.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not TypeDeclarationSyntax declaration
            || declaration is not RecordDeclarationSyntax { ParameterList: not null }
                && !user.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0))
            throw new NotSupportedException($"WRK119: Typed JSON reads need a record or a class with a parameterless constructor, not '{user}'.");
        ValidateJsonAttributes(user, typedRead: true);
        QueueUserType(user, source);
        _pendingJsonReaders.Enqueue((user, mode));
        HashSet<string> constructorProperties = declaration is RecordDeclarationSyntax { ParameterList: { } parameters }
            ? parameters.Parameters.Select(parameter => parameter.Identifier.ValueText).ToHashSet(StringComparer.Ordinal) : [];
        foreach (var property in JsonContractProperties(user).Where(property =>
            property.SetMethod?.DeclaredAccessibility == Accessibility.Public || constructorProperties.Contains(property.Name)))
            RegisterJsonValueType(property.Type, source, mode);
    }

    private static ITypeSymbol? JsonElementType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => array.ElementType,
        INamedTypeSymbol named => named.OriginalDefinition.ToDisplayString() switch
        {
            "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>"
                or "System.Collections.Generic.IReadOnlyList<T>" or "System.Collections.Generic.ICollection<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.IEnumerable<T>"
                or "System.Collections.Generic.HashSet<T>" => named.TypeArguments[0],
            "System.Collections.Generic.Dictionary<TKey, TValue>" or "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                when named.TypeArguments[0].SpecialType == SpecialType.System_String => named.TypeArguments[1],
            _ => null
        },
        _ => null
    };

    private static string JsonReaderName(int mode) => mode switch
    {
        JsonWebMode => "$fromJSON",
        JsonRowMode => "$fromRow",
        _ => "$fromQuery"
    };

    // Readers are static members assigned after every class is declared, so a type can gain a reader
    // for another source after its class was emitted.
    private void EmitPendingJsonReaders()
    {
        while (_pendingJsonReaders.Count != 0)
        {
            var (type, mode) = _pendingJsonReaders.Dequeue();
            using var specialization = SpecializationScope(type);
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences.Single().GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var parameters = declaration is RecordDeclarationSyntax { ParameterList: { } list }
                ? list.Parameters.ToArray() : [];
            _output.Append(_userTypes[type]).Append('.').Append(JsonReaderName(mode)).AppendLine(" = function (source) {");
            _output.AppendLine("  if (source == null) return source;");
            var arguments = parameters.Select(parameter =>
                JsonPropertyRead(UserTypeProperties(type).First(property => property.Name == parameter.Identifier.ValueText), parameter, mode));
            var constructor = type.InstanceConstructors.First(candidate => parameters.Length == 0
                ? candidate.Parameters.Length == 0
                : candidate.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is RecordDeclarationSyntax));
            _output.Append("  const result = ")
                .Append(UserConstruction(type, constructor, _userTypes[type], arguments.ToArray())).AppendLine(";");
            foreach (var property in JsonContractProperties(type).Where(property =>
                property.SetMethod?.DeclaredAccessibility == Accessibility.Public
                && !parameters.Any(parameter => parameter.Identifier.ValueText == property.Name)))
            {
                var key = JsonReadKey(property);
                _output.Append("  if (Object.hasOwn(source, ").Append(key).Append(")) ")
                    .Append(UserMemberAccess("result", property)).Append(" = ")
                    .Append(JsonValueExpression(property.Type, $"source[{key}]", mode)).AppendLine(";");
            }
            _output.AppendLine("  return result;");
            _output.AppendLine("};").AppendLine();
        }
    }

    private static string JsonReadKey(IPropertySymbol property) =>
        JsonSerializer.Serialize(JsonPropertyName(property) ?? LowerFirst(property.Name));

    private string JsonPropertyRead(IPropertySymbol property, SyntaxNode source, int mode)
    {
        var fallback = source is ParameterSyntax parameter
            && _model.GetDeclaredSymbol(parameter) is IParameterSymbol { HasExplicitDefaultValue: true } symbol
                ? ParameterDefaultValue(symbol, source)
                : DefaultFieldValue(property.Type, source);
        if (HasAttribute(property, "Workers.JsonIgnoreAttribute")) return fallback;
        var key = JsonReadKey(property);
        return $"Object.hasOwn(source, {key}) ? {JsonValueExpression(property.Type, $"source[{key}]", mode)} : {fallback}";
    }

    // JSON and structured-clone values keep their JavaScript types. SQLite rows store booleans as
    // 0 or 1. Query strings carry text, and a repeated name keeps every value.
    private string JsonValueExpression(ITypeSymbol type, string value, int mode)
    {
        type = UnwrapNullable(ResolveType(type));
        // Typed byte arrays use native byte buffers, whether read from JSON number arrays,
        // SQLite blobs, or structured-clone values that already contain a Uint8Array.
        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return $"(value => value == null || value instanceof Uint8Array ? value : new Uint8Array(value))({value})";
        if (JsonElementType(type) is { } element)
        {
            var isSet = type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>";
            var isList = IsListType(type);
            var converted = JsonValueExpression(element, "item", mode);
            if (converted == "item" && !isSet && !isList) return value;
            if (converted == "item" && isList)
                return $"(values => Array.isArray(values) ? {CreateList("values")} : values)({value})";
            var expression = type is INamedTypeSymbol { TypeArguments.Length: 2 }
                ? $"Object.fromEntries(Object.entries(values).map(([key, item]) => [key, {converted}]))"
                : $"Array.from(values, item => {converted})";
            if (isSet) expression = IsCompositeKey(element)
                ? KeyCollection((INamedTypeSymbol)type, _diagnosticNode ?? _model.SyntaxTree.GetRoot(), expression)
                : CreateSet(expression);
            if (isList) expression = CreateList(expression);
            return $"(values => values == null ? values : {expression})({value})";
        }
        if (mode == JsonQueryMode) value = $"(Array.isArray({value}) ? {value}[0] : {value})";
        if (type is INamedTypeSymbol user && _jsonReaders.Contains((user, mode)))
            return $"{_userTypes[user]}.{JsonReaderName(mode)}({value})";
        if (type is INamedTypeSymbol { Name: "Date", ContainingNamespace.Name: "Workers" })
            return $"(value => value == null ? value : new Date(value))({value})";
        if (mode == JsonQueryMode && type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return $"(value => value == null ? value : Number(value))({value})";
        if (mode == JsonQueryMode && type.SpecialType == SpecialType.System_Boolean)
            return $"(value => value == null ? value : value === \"true\")({value})";
        if (mode == JsonRowMode && type.SpecialType == SpecialType.System_Boolean)
            return $"(value => value == null ? value : Boolean(value))({value})";
        return value;
    }

    private string? JsonBoundaryConverter(ITypeSymbol type, SyntaxNode source, int mode)
    {
        RegisterJsonValueType(type, source, mode);
        var value = JsonValueExpression(type, "value", mode);
        return value == "value" ? null : $"value => value == null ? value : {value}";
    }

    private static List<IPropertySymbol> JsonContractProperties(INamedTypeSymbol type) =>
        UserTypeProperties(type).Where(IsJsonContractProperty).ToList();

    private static IEnumerable<IPropertySymbol> UserTypeProperties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && IsUserInstanceType(current); current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                if (seen.Add(property.Name)) yield return property;
    }

    private static bool IsJsonContractProperty(IPropertySymbol property) =>
        property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && !property.IsIndexer
        && !HasAttribute(property, "Workers.JsonIgnoreAttribute");

    private static void ValidateJsonAttributes(INamedTypeSymbol type, bool typedRead = false)
    {
        var legacy = type.GetMembers().Prepend(type).SelectMany(member => member.GetAttributes())
            .FirstOrDefault(attribute => attribute.AttributeClass?.ContainingNamespace.ToDisplayString()
                == "System.Text.Json.Serialization");
        if (legacy is not null)
            throw new NotSupportedException($"WRK119: '{legacy.AttributeClass}' is outside the Workers JSON contract. Use Workers.JsonName or Workers.JsonIgnore.");
        var collision = JsonContractProperties(type)
            .GroupBy(property => JsonPropertyName(property) ?? LowerFirst(property.Name), StringComparer.Ordinal)
            // Case-distinct C# properties remain usable internally; typed reads need an
            // unambiguous wire name, and explicit JsonName collisions are always errors.
            .FirstOrDefault(group => group.Count() > 1
                && (typedRead || group.Any(property => JsonPropertyName(property) is not null)));
        if (collision is not null)
            throw new NotSupportedException($"WRK119: User type '{type}' contains conflicting JSON property name '{collision.Key}'.");
    }
}
