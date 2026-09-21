using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
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
        if (_jsonMaterializedUserTypes.Contains(type))
            EmitUserJsonMaterializer(type, declaration);
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
            {
                var jsonName = JsonPropertyName(property) ?? property.Name;
                return $"{JavaScriptObjectKey(jsonName)}: {UserMemberAccess("this", property)}";
            })))
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

}
