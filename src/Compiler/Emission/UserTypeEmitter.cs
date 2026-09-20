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

    private static bool IsJavaScriptPropertyIdentifier(string value) => value.Length != 0
        && (char.IsAsciiLetter(value[0]) || value[0] is '_' or '$')
        && value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '$');

    private static string JavaScriptObjectKey(string value) => IsJavaScriptPropertyIdentifier(value)
        ? value
        : JsonSerializer.Serialize(value);

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

        var used = new HashSet<string>(["constructor", "toJSON"], StringComparer.Ordinal);
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

    private void EmitUserCode()
    {
        while (_pendingUserMethods.Count != 0 || _pendingUserTypes.Count != 0)
        {
            EmitUserMethods();
            while (_pendingUserTypes.Count != 0)
            {
                var type = _pendingUserTypes.Dequeue();
                if (_emittedUserTypes.Add(type))
                    EmitUserType(type);
            }
        }
    }

    private void EmitUserType(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length != 1
            || type.DeclaringSyntaxReferences[0].GetSyntax() is not TypeDeclarationSyntax declaration)
            throw new NotSupportedException($"WRK119: User type '{type}' must have one class or record declaration.");
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        ValidateUserType(type, declaration);

        _output.Append("class ").Append(_userTypes[type]).AppendLine(" {");
        EmitUserConstructor(type, declaration);
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>().Where(property =>
                     !IsAutoProperty(property)))
            EmitComputedProperty(property);
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>().Where(method =>
                     _model.GetDeclaredSymbol(method)?.IsStatic == false))
            EmitUserInstanceMethod(method);
        EmitUserJsonProjection(type, declaration);
        _output.AppendLine("}").AppendLine();
    }

    private void EmitUserJsonProjection(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var properties = new List<IPropertySymbol>();
        if (declaration is RecordDeclarationSyntax { ParameterList: { } parameters })
            foreach (var parameter in parameters.Parameters)
            {
                var parameterSymbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
                properties.Add(type.GetMembers(parameterSymbol.Name).OfType<IPropertySymbol>().Single());
            }
        properties.AddRange(declaration.Members.OfType<PropertyDeclarationSyntax>()
            .Select(property => (IPropertySymbol)_model.GetDeclaredSymbol(property)!)
            .Where(property => property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null));
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        properties = properties.Where(property => seen.Add(property)
            && !HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute")).ToList();

        _output.AppendLine("  toJSON() {");
        _output.Append("    return { ")
            .Append(string.Join(", ", properties.Select(property =>
                $"{JavaScriptObjectKey(UserMemberName(property))}: {UserMemberAccess("this", property)}")))
            .AppendLine(" };");
        _output.AppendLine("  }");
    }

    private void EmitUserConstructor(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>().SingleOrDefault();
        var record = declaration as RecordDeclarationSyntax;
        var parameters = constructor?.ParameterList.Parameters ?? record?.ParameterList?.Parameters ?? default;
        _output.Append("  constructor(")
            .Append(parameters.Count == 0 ? "" : string.Join(", ", parameters.Select(ParameterDeclaration)))
            .AppendLine(") {");

        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
            foreach (var variable in field.Declaration.Variables)
            {
                var symbol = (IFieldSymbol)_model.GetDeclaredSymbol(variable)!;
                if (symbol.IsStatic) continue;
                _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                    .Append(variable.Initializer is null
                        ? DefaultFieldValue(symbol.Type, variable)
                        : Expression(variable.Initializer.Value)).AppendLine(";");
            }

        if (record?.ParameterList is not null)
            foreach (var parameter in record.ParameterList.Parameters)
            {
                var parameterSymbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
                var property = type.GetMembers(parameterSymbol.Name).OfType<IPropertySymbol>().Single();
                _output.Append("    ").Append(UserMemberAccess("this", property)).Append(" = ")
                    .Append(ParameterName(parameter)).AppendLine(";");
            }

        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>().Where(IsAutoProperty))
        {
            var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
            _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                .Append(property.Initializer is null
                    ? DefaultFieldValue(symbol.Type, property)
                    : Expression(property.Initializer.Value)).AppendLine(";");
        }

        if (constructor?.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in constructor?.Body?.Statements ?? [])
                EmitStatement(statement, 2);
        _output.AppendLine("  }");
    }

    private void EmitComputedProperty(PropertyDeclarationSyntax property)
    {
        var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
        var name = UserMemberName(symbol);
        _output.Append("  get ").Append(IsJavaScriptPropertyIdentifier(name)
                ? name
                : $"[{JsonSerializer.Serialize(name)}]")
            .AppendLine("() {");
        if (property.ExpressionBody is not null)
            _output.Append("    return ").Append(Expression(property.ExpressionBody.Expression)).AppendLine(";");
        else
        {
            var getter = property.AccessorList!.Accessors.Single(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
            if (getter.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(getter.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in getter.Body?.Statements ?? [])
                    EmitStatement(statement, 2);
        }
        _output.AppendLine("  }");
    }

    private void EmitUserInstanceMethod(MethodDeclarationSyntax method)
    {
        var symbol = (IMethodSymbol)_model.GetDeclaredSymbol(method)!;
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        var isIterator = IsIterator(method);
        _output.Append("  ").Append(isAsync ? "async " : "").Append(isIterator ? "*" : "")
            .Append(UserInstanceMethodName(symbol)).Append('(')
            .Append(string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration))).AppendLine(") {");
        if (method.ExpressionBody is not null)
            _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in method.Body?.Statements ?? [])
                EmitStatement(statement, 2);
        _output.AppendLine("  }");
    }

    private static bool IsAutoProperty(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList is not null
        && property.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);

    private static void ValidateUserType(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        if (type.IsAbstract || type.IsStatic || type.Arity != 0
            || type.BaseType?.SpecialType != SpecialType.System_Object)
            throw new NotSupportedException(
                $"WRK119: User type '{type}' must be a non-abstract, non-generic class or record without inheritance.");
        ValidateJsonAttributes(type);
        if (declaration is RecordDeclarationSyntax { ParameterList: { } recordParameters }
            && recordParameters.Parameters.Any(parameter =>
                LowerFirst(parameter.Identifier.ValueText) == "toJSON"))
            throw new NotSupportedException($"WRK119: User type '{type}' contains a property that conflicts with generated JavaScript behavior.");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1 || constructors.Any(constructor =>
                constructor.Initializer is not null
                || constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                || constructor.Modifiers.Any(SyntaxKind.ExternKeyword)
                || constructor.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.RefKeyword)
                    || modifier.IsKind(SyntaxKind.OutKeyword)
                    || modifier.IsKind(SyntaxKind.InKeyword)
                    || modifier.IsKind(SyntaxKind.ParamsKeyword)))))
            throw new NotSupportedException($"WRK119: User type '{type}' must have at most one constructor without constructor chaining.");

        var unsupported = declaration.Members.FirstOrDefault(member => member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable =>
                variable.Parent?.Parent is not FieldDeclarationSyntax
                || field.Modifiers.Any(SyntaxKind.ConstKeyword)
                || field.Modifiers.Any(SyntaxKind.StaticKeyword)),
            ConstructorDeclarationSyntax => false,
            MethodDeclarationSyntax method => !IsSupportedUserMethod(method),
            PropertyDeclarationSyntax property => !IsSupportedUserProperty(property),
            _ => true
        });
        if (unsupported is not null)
            throw new NotSupportedException($"WRK119: User type '{type}' contains an unsupported member: {unsupported}");
    }

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

        var properties = type.GetMembers().OfType<IPropertySymbol>().Select(property => new
        {
            Property = property,
            Name = JsonPropertyName(property) ?? LowerFirst(property.Name),
            Explicit = JsonPropertyName(property) is not null
        }).ToArray();
        var collision = properties.GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1 && group.Any(property => property.Explicit));
        if (collision is not null)
            throw new NotSupportedException(
                $"WRK119: User type '{type}' contains conflicting JSON property name '{collision.Key}'.");
    }

    private static bool IsSupportedUserMethod(MethodDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || declaration.Modifiers.Any(SyntaxKind.ExternKeyword)
            || declaration.Modifiers.Any(SyntaxKind.VirtualKeyword)
            || declaration.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return false;
        return declaration.ParameterList.Parameters.All(parameter =>
            parameter.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.RefKeyword)
                && !modifier.IsKind(SyntaxKind.OutKeyword)
                && !modifier.IsKind(SyntaxKind.InKeyword)
                && !modifier.IsKind(SyntaxKind.ParamsKeyword)));
    }

    private static bool IsSupportedUserProperty(PropertyDeclarationSyntax property)
    {
        var emittedName = LowerFirst(property.Identifier.ValueText);
        if (emittedName is "constructor" or "toJSON") return false;
        if (property.Modifiers.Any(SyntaxKind.StaticKeyword)
            || property.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || property.Modifiers.Any(SyntaxKind.VirtualKeyword)
            || property.Modifiers.Any(SyntaxKind.OverrideKeyword))
            return false;
        if (IsAutoProperty(property)) return true;
        if (property.Initializer is not null) return false;
        if (property.ExpressionBody is not null) return true;
        return property.AccessorList?.Accessors is [{ } getter]
            && getter.IsKind(SyntaxKind.GetAccessorDeclaration)
            && (getter.Body is not null || getter.ExpressionBody is not null);
    }
}
