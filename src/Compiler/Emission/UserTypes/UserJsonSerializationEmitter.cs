using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

// JsonSerializer.Serialize follows the System.Text.Json default contract: CLR property names, ISO
// dates with offsets, constant-format TimeSpans, base64 byte arrays and the default escaping rules.
// Values are projected to that shape using their static C# types before JSON.stringify runs, so the
// web-named toJSON used by the Workers APIs does not leak into JsonSerializer output.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _clrJsonProjections = new(SymbolEqualityComparer.Default);
    private readonly Queue<INamedTypeSymbol> _pendingClrJsonProjections = new();

    private string JsonClrSerialize(ITypeSymbol type, string value) =>
        $"{_helpers.Require(JavaScriptHelper.JsonSerializeClr)}({ClrJsonProjection(type, value)})";

    private string ClrJsonProjection(ITypeSymbol type, string value)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return ClrJsonProjection(nullable.TypeArguments[0], value);
        if (type.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            return $"{ClrJsonHelper("jsonClrNumber", value)[..^1]}, {(type.SpecialType == SpecialType.System_Single ? "true" : "false")})";
        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return ClrJsonHelper("jsonClrBytes", value);
        switch (type.ToDisplayString())
        {
            case "System.DateTimeOffset":
                return ClrJsonHelper("jsonClrDate", value);
            case "System.TimeSpan":
                return ClrJsonHelper("jsonClrTimeSpan", value);
        }
        if (JsonElementType(type) is { } element)
        {
            var item = _names.Get("clr-json-item", "item");
            var projected = ClrJsonProjection(element, item);
            var set = type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>";
            if (type is INamedTypeSymbol { TypeArguments.Length: 2 })
                return projected == item
                    ? value
                    : $"(values => values == null ? null : Object.fromEntries(Object.entries(values).map(([key, {item}]) => [key, {projected}])))({value})";
            if (projected == item && !set)
                return value;
            return $"(values => values == null ? null : Array.from(values, {item} => {projected}))({value})";
        }
        if (type is INamedTypeSymbol { IsAnonymousType: true } anonymous)
        {
            var properties = anonymous.GetMembers().OfType<IPropertySymbol>().ToArray();
            var projections = properties
                .Select(property => (property, projected: ClrJsonProjection(property.Type, $"anonymous{AnonymousAccess(property.Name)}")))
                .ToArray();
            if (projections.All(item => item.projected == $"anonymous{AnonymousAccess(item.property.Name)}"))
                return value;
            return $"(anonymous => anonymous == null ? null : {{ {string.Join(", ", projections.Select(item =>
                $"{JavaScriptObjectKey(item.property.Name)}: {item.projected}"))} }})({value})";
        }
        if (type is INamedTypeSymbol user && IsUserInstanceType(user) && !IsException(user))
            return $"{ClrJsonProjectionFunction(user)}({value})";
        return value;
    }

    private static string AnonymousAccess(string name) =>
        name != "__proto__" && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '$')
            && !char.IsAsciiDigit(name[0])
            ? $".{name}"
            : $"[{JsonSerializer.Serialize(name)}]";

    private string ClrJsonHelper(string name, string value)
    {
        _helpers.Require(JavaScriptHelper.JsonSerializeClr);
        return $"{_helpers.Name(name)}({value})";
    }

    private string ClrJsonProjectionFunction(INamedTypeSymbol type)
    {
        type = type.OriginalDefinition;
        if (_clrJsonProjections.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"clr-json:{type.ToDisplayString()}", $"clrJson${type.Name}");
        _clrJsonProjections.Add(type, name);
        _pendingClrJsonProjections.Enqueue(type);
        return name;
    }

    private bool EmitPendingClrJsonProjections()
    {
        var emitted = false;
        while (_pendingClrJsonProjections.TryDequeue(out var type))
        {
            emitted = true;
            var declaration = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var properties = JsonProjectionProperties(type, declaration);
            _output.Append("function ").Append(_clrJsonProjections[type]).AppendLine("(value) {");
            _output.AppendLine("  if (value == null) return null;");
            _output.Append("  return { ").Append(string.Join(", ", properties.Select(property =>
                    $"{JavaScriptObjectKey(JsonPropertyName(property) ?? property.Name)}: " +
                    ClrJsonProjection(property.Type, UserMemberAccess("value", property)))))
                .AppendLine(" };");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }

    // Serialized members in System.Text.Json order: positional record members first, then declared properties.
    private List<IPropertySymbol> JsonProjectionProperties(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var properties = new List<IPropertySymbol>();
        if (declaration is RecordDeclarationSyntax { ParameterList: { } parameters })
            foreach (var parameter in parameters.Parameters)
                properties.Add(type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().Single());
        properties.AddRange(declaration.Members.OfType<PropertyDeclarationSyntax>()
            .Select(property => (IPropertySymbol)_model.GetDeclaredSymbol(property)!)
            .Where(property => property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null
                && !property.IsStatic));
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        return properties.Where(property => seen.Add(property)
            && !HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute")).ToList();
    }
}
