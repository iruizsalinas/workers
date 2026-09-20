using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        properties = properties.Where(property => seen.Add(property)).ToList();

        _output.AppendLine("  toJSON() {");
        _output.Append("    return { ")
            .Append(string.Join(", ", properties.Select(property =>
                $"{LowerFirst(property.Name)}: this.{LowerFirst(property.Name)}")))
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
                _output.Append("    this.").Append(UserIdentifier(symbol, symbol.Name)).Append(" = ")
                    .Append(variable.Initializer is null
                        ? DefaultFieldValue(symbol.Type, variable)
                        : Expression(variable.Initializer.Value)).AppendLine(";");
            }

        if (record?.ParameterList is not null)
            foreach (var parameter in record.ParameterList.Parameters)
            {
                var parameterSymbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
                var property = type.GetMembers(parameterSymbol.Name).OfType<IPropertySymbol>().Single();
                _output.Append("    this.").Append(LowerFirst(property.Name)).Append(" = ")
                    .Append(ParameterName(parameter)).AppendLine(";");
            }

        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>().Where(IsAutoProperty))
        {
            var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
            _output.Append("    this.").Append(LowerFirst(symbol.Name)).Append(" = ")
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
        _output.Append("  get ").Append(LowerFirst(symbol.Name)).AppendLine("() {");
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
