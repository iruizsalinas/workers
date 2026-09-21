using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsUserInstanceType(INamedTypeSymbol type) =>
        !type.IsStatic
        && !IsGeneratedInstanceType(type)
        && type.DeclaringSyntaxReferences.Length != 0
        && type.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ClassDeclarationSyntax or RecordDeclarationSyntax);

    private static bool RequiresUserClass(INamedTypeSymbol type)
    {
        if (!type.IsRecord) return true;
        if (type.GetMembers().OfType<IPropertySymbol>().Any(property =>
                HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute")
                || HasAttribute(property, "System.Text.Json.Serialization.JsonPropertyNameAttribute")))
            return true;
        return type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is RecordDeclarationSyntax record
            && record.Members.Any(member => member is FieldDeclarationSyntax
                or ConstructorDeclarationSyntax
                or PropertyDeclarationSyntax
                or MethodDeclarationSyntax));
    }

    private string QueueUserType(INamedTypeSymbol type, SyntaxNode source)
    {
        type = type.OriginalDefinition;
        if (!IsUserInstanceType(type))
            throw UnsupportedSymbol(type, source);
        if (_userTypes.TryGetValue(type, out var existing))
            return existing;
        var name = _names.Get($"user-type:{type.ToDisplayString()}", type.Name);
        _userTypes.Add(type, name);
        _pendingUserTypes.Enqueue(type);
        return name;
    }

    private string UserInstanceMethodName(IMethodSymbol method)
    {
        method = method.OriginalDefinition;
        if (_userInstanceMethods.TryGetValue(method, out var existing))
            return existing;
        var preferred = LowerNativeMethodName(method.Name);
        var name = _names.Get(
            $"instance-method:{method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}",
            $"cs${method.ContainingType.Name}${preferred}");
        _userInstanceMethods.Add(method, name);
        return name;
    }

    private string UserMemberName(ISymbol member)
    {
        PrepareUserMemberNames(member.ContainingType);
        return _userMemberNames.TryGetValue(member, out var name)
            ? name
            : throw new InvalidOperationException($"User member '{member}' was not assigned a JavaScript name.");
    }

    private string UserMemberAccess(string receiver, ISymbol member)
    {
        var name = UserMemberName(member);
        return IsJavaScriptPropertyIdentifier(name)
            ? $"{receiver}.{name}"
            : $"{receiver}[{JsonSerializer.Serialize(name)}]";
    }

    private static bool IsJavaScriptPropertyIdentifier(string value) => value != "__proto__" && value.Length != 0
        && (char.IsAsciiLetter(value[0]) || value[0] is '_' or '$')
        && value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '$');

    private static string JavaScriptObjectKey(string value) => value == "__proto__"
        ? $"[{JsonSerializer.Serialize(value)}]"
        : IsJavaScriptPropertyIdentifier(value) ? value : JsonSerializer.Serialize(value);

    private static bool HasAttribute(ISymbol symbol, string name) => symbol.GetAttributes()
        .Any(attribute => attribute.AttributeClass?.ToDisplayString() == name);

    private static string? JsonPropertyName(ISymbol symbol) => symbol.GetAttributes()
        .SingleOrDefault(attribute => attribute.AttributeClass?.ToDisplayString()
            == "System.Text.Json.Serialization.JsonPropertyNameAttribute")
        ?.ConstructorArguments.SingleOrDefault().Value as string;

    private void PrepareUserMemberNames(INamedTypeSymbol type)
    {
        type = type.OriginalDefinition;
        if (!_preparedUserMemberNames.Add(type)) return;

        var used = new HashSet<string>(["__proto__", "constructor", "toJSON"], StringComparer.Ordinal);
        var members = type.GetMembers()
            .Where(member => !member.IsStatic && member.DeclaringSyntaxReferences.Length != 0)
            .OrderBy(member => member is IPropertySymbol ? 0 : 1)
            .ThenBy(member => member.DeclaringSyntaxReferences[0].Span.Start);
        foreach (var member in members)
        {
            var preferred = member switch
            {
                IPropertySymbol property => JsonPropertyName(property) ?? LowerFirst(property.Name),
                IFieldSymbol field => field.Name,
                _ => null
            };
            if (preferred is null) continue;
            if (member is IPropertySymbol && JsonPropertyName(member) is not null
                && (preferred is "constructor" or "toJSON" || used.Contains(preferred)))
                throw new NotSupportedException(
                    $"WRK119: User type '{type}' contains a conflicting JSON property name '{preferred}'.");
            var name = preferred;
            for (var suffix = 2; !used.Add(name); suffix++)
                name = $"{preferred}${suffix}";
            _userMemberNames.Add(member, name);
        }
    }

}
