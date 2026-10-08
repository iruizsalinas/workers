using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// User types are materialized from JSON by a generated static $fromJSON(value, mode) method:
//   mode 0 follows System.Text.Json defaults (case-sensitive CLR names, strict value types),
//   mode 1 follows JsonSerializerDefaults.Web (case-insensitive camelCase names, quoted numbers),
//   mode 2 reads storage rows such as D1 results and structured clones (case-insensitive names).
// JsonSerializer requests strict registration, which rejects unsupported member types at compile
// time. Workers API reads register leniently and pass unsupported members through unchanged.
internal sealed partial class JavaScriptEmitter
{
    private const int JsonClrMode = 0;
    private const int JsonWebMode = 1;
    private const int JsonRowMode = 2;
    private const int JsonQueryMode = 3;

    private bool RegisterJsonMaterializer(INamedTypeSymbol type, SyntaxNode source, bool strict = true)
    {
        type = type.OriginalDefinition;
        if (_jsonMaterializedUserTypes.Contains(type) && (!strict || _jsonStrictUserTypes.Contains(type)))
            return true;
        var declaration = type.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as TypeDeclarationSyntax;
        var constructible = declaration is RecordDeclarationSyntax { ParameterList: not null }
            || declaration is not null && type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);
        if (!strict && (declaration is null || !constructible || IsException(type)))
            return false;
        ValidateJsonAttributes(type);
        if (declaration is null)
            throw UnsupportedSymbol(type, source);
        if (!constructible)
            throw new NotSupportedException(
                $"WRK119: User type '{type}' needs a parameterless constructor for JSON deserialization.");
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(property =>
                     property.DeclaredAccessibility == Accessibility.Public && IsJsonRequired(property, type)))
            if (property.DeclaredAccessibility != Accessibility.Public || property.GetMethod is null
                || property.SetMethod?.DeclaredAccessibility != Accessibility.Public
                || HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
                throw new NotSupportedException(
                    $"WRK119: Required JSON property '{property}' must be public, writable and included in the JSON contract.");
        QueueUserType(type, source);
        _jsonMaterializedUserTypes.Add(type);
        if (strict) _jsonStrictUserTypes.Add(type);
        foreach (var property in JsonReadableProperties(type, declaration))
            RegisterJsonValueType(property.Type, source, strict);
        return true;
    }

    private static IEnumerable<IPropertySymbol> JsonReadableProperties(INamedTypeSymbol type, TypeDeclarationSyntax declaration) =>
        JsonContractProperties(type).Where(property =>
            property.SetMethod?.DeclaredAccessibility == Accessibility.Public
            || declaration is RecordDeclarationSyntax { ParameterList: { } parameters }
               && parameters.Parameters.Any(parameter => parameter.Identifier.ValueText == property.Name));

    private void RegisterJsonValueType(ITypeSymbol type, SyntaxNode source, bool strict)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (JsonElementType(type) is { } element)
        {
            RegisterJsonValueType(element, source, strict);
            return;
        }
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
        {
            if (!RegisterJsonMaterializer(user, source, strict) && strict)
                throw new NotSupportedException($"WRK119: JSON deserialization does not support member type '{type}'.");
            return;
        }
        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumeration)
        {
            RegisterJsonValueType(enumeration.EnumUnderlyingType!, source, strict);
            return;
        }
        if (JsonScalarKind(type) is not null || type.SpecialType == SpecialType.System_Object || IsJsonNodeType(type)
            || type.ToDisplayString() == "System.Text.Json.JsonElement") return;
        if (strict)
            throw new NotSupportedException($"WRK119: JSON deserialization does not support member type '{type}'.");
    }

    // Element type of arrays, lists, read-only collection interfaces, sets, and string-keyed dictionaries.
    private static ITypeSymbol? JsonElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol { ElementType.SpecialType: not SpecialType.System_Byte } array)
            return array.ElementType;
        if (type is not INamedTypeSymbol named) return null;
        return named.OriginalDefinition.ToDisplayString() switch
        {
            "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>"
                or "System.Collections.Generic.IReadOnlyList<T>" or "System.Collections.Generic.ICollection<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.IEnumerable<T>"
                or "System.Collections.Generic.HashSet<T>" => named.TypeArguments[0],
            "System.Collections.Generic.Dictionary<TKey, TValue>" or "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                when named.TypeArguments[0].SpecialType == SpecialType.System_String => named.TypeArguments[1],
            _ => null
        };
    }

    private static string? JsonScalarKind(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_String => "0",
        SpecialType.System_Boolean => "1",
        SpecialType.System_SByte => "2, -128, 127",
        SpecialType.System_Byte => "2, 0, 255",
        SpecialType.System_Int16 => "2, -32768, 32767",
        SpecialType.System_UInt16 => "2, 0, 65535",
        SpecialType.System_Int32 => "2, -2147483648, 2147483647",
        SpecialType.System_UInt32 => "2, 0, 4294967295",
        SpecialType.System_Int64 => "2, -9007199254740991, 9007199254740991",
        SpecialType.System_UInt64 => "2, 0, 9007199254740991",
        SpecialType.System_Single => "3",
        SpecialType.System_Double => "4",
        SpecialType.System_Char => "5",
        _ when type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => "6",
        _ => type.ToDisplayString() switch
        {
            "System.DateTimeOffset" or "System.DateTime" => "7",
            "System.Guid" => "8",
            "System.DateOnly" => "9",
            "System.TimeOnly" => "10",
            _ => null
        }
    };

    private void EmitUserJsonMaterializer(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var recordParameters = declaration is RecordDeclarationSyntax { ParameterList: { } list }
            ? list.Parameters.Select(parameter => parameter.Identifier.ValueText).ToHashSet(StringComparer.Ordinal)
            : [];
        _output.AppendLine("  static $fromJSON(value, mode = 0) {");
        _output.AppendLine("    if (value == null) return null;");
        _output.AppendLine("    if (typeof value !== \"object\" || Array.isArray(value)) throw new TypeError(\"Expected a JSON object.\");");
        _helpers.Require(JavaScriptHelper.JsonDeserializeValue);
        _output.Append("    const source = mode === 0 ? value : ").Append(_helpers.Name("jsonFold")).AppendLine("(value);");
        foreach (var property in JsonContractProperties(type).Where(property => IsJsonRequired(property, type)))
            _output.Append("    if (!Object.hasOwn(source, ").Append(JsonReadKey(property))
                .Append(")) throw new TypeError(")
                .Append(JsonSerializer.Serialize($"Required JSON property '{property.Name}' is missing."))
                .AppendLine(");");
        var arguments = declaration is RecordDeclarationSyntax { ParameterList: { } parameters }
            ? parameters.Parameters.Select(parameter =>
            {
                var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().Single();
                var symbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
                var fallback = symbol.HasExplicitDefaultValue
                    ? LiteralConstant(symbol.ExplicitDefaultValue, parameter)
                    : DefaultFieldValue(property.Type, parameter);
                if (HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
                    return fallback;
                return JsonPropertyRead(property, "source", fallback);
            })
            : [];
        _output.Append("    const result = new ").Append(_userTypes[type]).Append('(')
            .Append(string.Join(", ", arguments)).AppendLine(");");
        foreach (var property in JsonContractProperties(type).Where(property =>
                     !recordParameters.Contains(property.Name)
                     && property.SetMethod?.DeclaredAccessibility == Accessibility.Public))
        {
            var key = JsonReadKey(property);
            _output.Append("    if (Object.hasOwn(source, ").Append(key).Append(")) ")
                .Append(UserMemberAccess("result", property)).Append(" = ")
                .Append(JsonValueExpression(property.Type, $"source[{key}]", "mode")).AppendLine(";");
        }
        _output.AppendLine("    return result;");
        _output.AppendLine("  }");
    }

    // CLR mode matches the exact contract name; the other modes match the camelCase name case-insensitively.
    private static string JsonReadKey(IPropertySymbol property)
    {
        var exact = JsonPropertyName(property) ?? property.Name;
        var folded = (JsonPropertyName(property) ?? LowerFirst(property.Name)).ToLowerInvariant();
        return exact == folded
            ? JsonSerializer.Serialize(exact)
            : $"(mode === 0 ? {JsonSerializer.Serialize(exact)} : {JsonSerializer.Serialize(folded)})";
    }

    private string JsonPropertyRead(IPropertySymbol property, string receiver, string fallback)
    {
        var key = JsonReadKey(property);
        return $"Object.hasOwn({receiver}, {key}) ? {JsonValueExpression(property.Type, $"{receiver}[{key}]", "mode")} : {fallback}";
    }

    // Converts a parsed JSON value to the representation of the given type. Returns the value unchanged
    // for types without a JSON contract; strict registration rejects those before emission.
    private string JsonValueExpression(ITypeSymbol type, string value, string mode)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return JsonHelperCall("jsonDeserializeNullable", value, $"item => {JsonValueExpression(nullable.TypeArguments[0], "item", mode)}");
        if (JsonElementType(type) is { } element)
        {
            var convert = $"item => {JsonValueExpression(element, "item", mode)}";
            if (type is INamedTypeSymbol { TypeArguments.Length: 2 })
                return JsonHelperCall("jsonDeserializeDictionary", value, convert);
            var array = JsonHelperCall("jsonDeserializeArray", value, convert, mode);
            return type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
                ? $"((items) => items === null ? null : new Set(items))({array})"
                : array;
        }
        if (type is INamedTypeSymbol user && IsUserInstanceType(user)
            && _jsonMaterializedUserTypes.Contains(user.OriginalDefinition))
            return $"{_userTypes[user.OriginalDefinition]}.$fromJSON({value}, {mode})";
        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumeration)
            return JsonValueExpression(enumeration.EnumUnderlyingType!, value, mode);
        if (IsJsonNodeType(type))
            return JsonNodeHelper("jsonNodeImport", value, JsonNodeKind(type).ToString());
        return JsonScalarKind(type) is { } kind ? JsonHelperCall("jsonDeserializeValue", value, mode, kind) : value;
    }

    private string JsonHelperCall(string helper, params string[] arguments)
    {
        _helpers.Require(JavaScriptHelper.JsonDeserializeValue);
        return $"{_helpers.Name(helper)}({string.Join(", ", arguments)})";
    }

    // A JavaScript function converting values read through a Workers API into the given type, or null
    // when the value can be used as is.
    private string? JsonBoundaryConverter(ITypeSymbol type, SyntaxNode source, int mode, bool missingIsDefault)
    {
        var target = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        RegisterJsonValueType(target, source, strict: false);
        var parameter = _names.Get($"json-boundary:{source.SyntaxTree.FilePath}:{source.SpanStart}", "value");
        var converted = JsonValueExpression(target, parameter, mode.ToString());
        if (converted == parameter) return null;
        var nullableResult = !type.IsValueType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        if (nullableResult)
            return $"{parameter} => {parameter} == null ? null : {converted}";
        if (missingIsDefault && (type.TypeKind == TypeKind.Enum
                || type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char
                    or >= SpecialType.System_SByte and <= SpecialType.System_Double))
            return $"{parameter} => {parameter} == null ? {DefaultFieldValue(type, source)} : {converted}";
        return $"{parameter} => {converted}";
    }

    private static List<IPropertySymbol> JsonContractProperties(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IPropertySymbol>()
            .Where(IsJsonContractProperty)
            .ToList();

    private static bool IsJsonContractProperty(IPropertySymbol property) =>
        property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && property.GetMethod is not null
        && !HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute");

    // Record copy constructors set required members, but are not JSON constructors.
    private static bool IsJsonRequired(IPropertySymbol property, INamedTypeSymbol type) =>
        HasAttribute(property, "System.Text.Json.Serialization.JsonRequiredAttribute")
        || property.IsRequired && !type.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared
            && !IsUserRecordCopyConstructor(constructor)
            && HasAttribute(constructor, "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"));

    private static void ValidateJsonAttributes(INamedTypeSymbol type)
    {
        foreach (var symbol in type.GetMembers().Where(member => !member.IsImplicitlyDeclared).Prepend(type))
        foreach (var attribute in symbol.GetAttributes().Where(attribute =>
                     attribute.AttributeClass?.ContainingNamespace.ToDisplayString()
                     == "System.Text.Json.Serialization"))
        {
            var name = attribute.AttributeClass!.Name;
            var supported = symbol is IPropertySymbol && name switch
            {
                "JsonPropertyNameAttribute" => attribute.ConstructorArguments is
                    [{ Kind: TypedConstantKind.Primitive, Value: string }]
                    && attribute.NamedArguments.Length == 0,
                "JsonIgnoreAttribute" => attribute.ConstructorArguments.Length == 0
                    && attribute.NamedArguments.Length == 0,
                "JsonRequiredAttribute" => attribute.ConstructorArguments.Length == 0
                    && attribute.NamedArguments.Length == 0,
                _ => false
            };
            if (!supported)
                throw new NotSupportedException(
                    $"WRK119: User type '{type}' uses unsupported JSON attribute '{attribute.AttributeClass}'.");
        }

        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(IsJsonContractProperty)
            .Select(property => new
        {
            Property = property,
            Name = JsonPropertyName(property) ?? property.Name
        }).ToArray();
        var collision = properties.GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
            throw new NotSupportedException(
                $"WRK119: User type '{type}' contains conflicting JSON property name '{collision.Key}'.");
    }

}
