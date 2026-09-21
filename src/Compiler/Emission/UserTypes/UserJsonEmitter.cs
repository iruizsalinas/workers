using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private void RegisterJsonMaterializer(INamedTypeSymbol type, SyntaxNode source)
    {
        type = type.OriginalDefinition;
        if (!_jsonMaterializedUserTypes.Add(type)) return;
        ValidateJsonAttributes(type);
        QueueUserType(type, source);
        var declaration = type.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as TypeDeclarationSyntax
            ?? throw UnsupportedSymbol(type, source);
        if (declaration is not RecordDeclarationSyntax { ParameterList: not null }
            && type.InstanceConstructors.All(constructor => constructor.Parameters.Length != 0))
            throw new NotSupportedException(
                $"WRK119: User type '{type}' needs a parameterless constructor for JSON deserialization.");
        foreach (var property in JsonContractProperties(type).Where(property =>
                     property.SetMethod?.DeclaredAccessibility == Accessibility.Public
                     || declaration is RecordDeclarationSyntax { ParameterList: not null }
                        && declaration is RecordDeclarationSyntax record
                        && record.ParameterList!.Parameters.Any(parameter => parameter.Identifier.ValueText == property.Name)))
            RegisterJsonValueType(property.Type, source);
    }

    private void RegisterJsonValueType(ITypeSymbol type, SyntaxNode source)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type is IArrayTypeSymbol array)
        {
            RegisterJsonValueType(array.ElementType, source);
            return;
        }
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
        {
            RegisterJsonValueType(named.TypeArguments[0], source);
            return;
        }
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
        {
            RegisterJsonMaterializer(user, source);
            return;
        }
        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum
            || type.ToDisplayString() == "System.Text.Json.JsonElement") return;
        throw new NotSupportedException($"WRK119: JSON deserialization does not support member type '{type}'.");
    }

    private void EmitUserJsonMaterializer(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var properties = JsonContractProperties(type);
        var recordParameters = declaration is RecordDeclarationSyntax { ParameterList: { } list }
            ? list.Parameters.Select(parameter => parameter.Identifier.ValueText).ToHashSet(StringComparer.Ordinal)
            : [];
        _output.AppendLine("  static $fromJSON(value) {");
        _output.AppendLine("    if (value == null) return null;");
        _output.AppendLine("    if (typeof value !== \"object\" || Array.isArray(value)) throw new TypeError(\"Expected a JSON object.\");");
        var arguments = declaration is RecordDeclarationSyntax { ParameterList: { } parameters }
            ? parameters.Parameters.Select(parameter =>
            {
                var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().Single();
                if (HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
                    return DefaultFieldValue(property.Type, parameter);
                return JsonPropertyRead(property, "value", DefaultFieldValue(property.Type, parameter));
            })
            : [];
        _output.Append("    const result = new ").Append(_userTypes[type]).Append('(')
            .Append(string.Join(", ", arguments)).AppendLine(");");
        foreach (var property in properties.Where(property =>
                     !recordParameters.Contains(property.Name)
                     && property.SetMethod?.DeclaredAccessibility == Accessibility.Public))
        {
            var jsonName = JsonPropertyName(property) ?? property.Name;
            _output.Append("    if (Object.hasOwn(value, ").Append(JsonSerializer.Serialize(jsonName)).Append(")) ")
                .Append(UserMemberAccess("result", property)).Append(" = ")
                .Append(JsonValueExpression(property.Type, $"value[{JsonSerializer.Serialize(jsonName)}]")).AppendLine(";");
        }
        _output.AppendLine("    return result;");
        _output.AppendLine("  }");
    }

    private string JsonPropertyRead(IPropertySymbol property, string receiver, string fallback)
    {
        var name = JsonPropertyName(property) ?? property.Name;
        var value = $"{receiver}[{JsonSerializer.Serialize(name)}]";
        return $"Object.hasOwn({receiver}, {JsonSerializer.Serialize(name)}) ? {JsonValueExpression(property.Type, value)} : {fallback}";
    }

    private string JsonValueExpression(ITypeSymbol type, string value)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return JsonValueExpression(nullable.TypeArguments[0], value);
        if (type is IArrayTypeSymbol array)
            return $"{value} == null ? null : Array.from({value}, item => {JsonValueExpression(array.ElementType, "item")})";
        if (type is INamedTypeSymbol list
            && list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
            return $"{value} == null ? null : Array.from({value}, item => {JsonValueExpression(list.TypeArguments[0], "item")})";
        if (type is INamedTypeSymbol user && IsUserInstanceType(user))
            return $"{_userTypes[user.OriginalDefinition]}.$fromJSON({value})";
        return value;
    }

    private static List<IPropertySymbol> JsonContractProperties(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null
                && !HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
            .ToList();

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
                _ => false
            };
            if (!supported)
                throw new NotSupportedException(
                    $"WRK119: User type '{type}' uses unsupported JSON attribute '{attribute.AttributeClass}'.");
        }

        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(property => !HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
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
