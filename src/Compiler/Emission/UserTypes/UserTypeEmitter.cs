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
        if (type.IsGenericType || UsesUserHierarchy(type) || type.AllInterfaces.Any(IsSourceInterface)) return true;
        if (type.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic)
            .GroupBy(property => LowerFirst(property.Name), StringComparer.Ordinal).Any(group => group.Skip(1).Any()))
            return true;
        if (type.GetMembers().OfType<IPropertySymbol>().Any(property =>
                LowerFirst(property.Name) is "__proto__" or "constructor" or "toJSON"
                || HasAttribute(property, "Workers.JsonIgnoreAttribute")
                || HasAttribute(property, "Workers.JsonNameAttribute")))
            return true;
        return type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is RecordDeclarationSyntax record
            && record.Members.Any(member => member is FieldDeclarationSyntax
                or ConstructorDeclarationSyntax
                or PropertyDeclarationSyntax
                or MethodDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax or EventFieldDeclarationSyntax));
    }

    private string QueueUserType(INamedTypeSymbol type, SyntaxNode source)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        if (!IsUserInstanceType(type))
            throw UnsupportedSymbol(type, source);
        if (_userTypes.TryGetValue(type, out var existing))
            return existing;
        CountSpecialization(type, source);
        var name = _names.Get($"user-type:{type.ToDisplayString()}", type.Name);
        _userTypes.Add(type, name);
        _pendingUserTypes.Enqueue(type);
        return name;
    }

    private string UserInstanceMethodName(IMethodSymbol method)
    {
        if (IsSourceInterface(method.ContainingType))
        {
            method = ResolveMethod(method);
            _requestedInterfaces.Add(method.ContainingType);
            return _names.Get($"interface-method:{method.ToDisplayString()}", $"interface${method.ContainingType.Name}${method.Name}");
        }
        while (method.OverriddenMethod is { } overridden) method = overridden;
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
        if (member.ContainingType is { TypeKind: TypeKind.Interface } contract)
        {
            contract = (INamedTypeSymbol)ResolveType(contract);
            _requestedInterfaces.Add(contract);
            return _names.Get($"interface-property:{contract}:{member.Name}", $"interface${contract.Name}${member.Name}");
        }
        if (member is IPropertySymbol property)
        {
            while (property.OverriddenProperty is { } overridden) property = overridden;
            member = property;
        }
        member = member.OriginalDefinition;
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

    private static AttributeData? JsonAttribute(ISymbol symbol, string name)
    {
        for (ISymbol? current = symbol; current is not null; current = (current as IPropertySymbol)?.OverriddenProperty)
            if (current.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == name) is { } attribute)
                return attribute;
        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string name) => JsonAttribute(symbol, name) is not null;

    private static string? JsonPropertyName(ISymbol symbol) => JsonAttribute(symbol, "Workers.JsonNameAttribute")
        ?.ConstructorArguments.SingleOrDefault().Value as string;

    private void PrepareUserMemberNames(INamedTypeSymbol type)
    {
        type = type.OriginalDefinition;
        if (!_preparedUserMemberNames.Add(type)) return;

        var used = new HashSet<string>(["__proto__", "constructor", "toJSON"], StringComparer.Ordinal);
        if (type.BaseType is { } parent && IsUserInstanceType(parent))
        {
            PrepareUserMemberNames(parent);
            for (var ancestor = parent; ancestor is not null && IsUserInstanceType(ancestor); ancestor = ancestor.BaseType)
                foreach (var name in _userMemberNames.Where(pair =>
                             SymbolEqualityComparer.Default.Equals(pair.Key.ContainingType, ancestor.OriginalDefinition)).Select(pair => pair.Value))
                    used.Add(name);
        }
        var members = type.GetMembers()
            .Where(member => !member.IsStatic && member.DeclaringSyntaxReferences.Length != 0)
            .OrderBy(member => member is IPropertySymbol property
                ? IsJsonContractProperty(property) ? 0 : 1
                : 2)
            .ThenBy(member => member.DeclaringSyntaxReferences[0].Span.Start);
        foreach (var member in members)
        {
            if (member is IPropertySymbol { OverriddenProperty: not null }) continue;
            var preferred = member switch
            {
                IPropertySymbol property => IsJsonContractProperty(property)
                    ? JsonPropertyName(property) ?? LowerFirst(property.Name)
                    : LowerFirst(property.Name),
                IFieldSymbol field => field.Name,
                IEventSymbol ev => $"event${ev.Name}",
                _ => null
            };
            if (preferred is null) continue;
            if (member is IPropertySymbol propertySymbol && IsJsonContractProperty(propertySymbol)
                && JsonPropertyName(member) is not null
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
