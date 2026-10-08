using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private void EmitUserCode()
    {
        while (_pendingUserMethods.Count != 0 || _pendingUserTypes.Count != 0
               || _pendingStaticHolders.Count != 0 || _pendingStaticGetters.Count != 0
               || _pendingClrJsonProjections.Count != 0 || _pendingRecordTextFunctions.Count != 0
               || _pendingRecordEqualityFunctions.Count != 0 || _pendingEnumTextFunctions.Count != 0)
        {
            EmitPendingEnumTextFunctions();
            EmitPendingRecordEqualityFunctions();
            EmitPendingRecordTextFunctions();
            EmitUserMethods();
            EmitPendingStaticState();
            EmitPendingClrJsonProjections();
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
        _diagnosticNode = declaration;
        ValidateUserType(type, declaration);

        _output.Append("class ").Append(_userTypes[type])
            .Append(IsUserException(type) ? " extends Error" : "").AppendLine(" {");
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

    // Workers APIs serialize with JSON.stringify, which calls toJSON. The names follow the web
    // contract (camelCase unless JsonPropertyName is present), matching plain records.
    private void EmitUserJsonProjection(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var members = JsonProjectionProperties(type, declaration)
            .Select(property => (Property: property, Name: JsonPropertyName(property) ?? LowerFirst(property.Name)))
            .ToList();
        _output.AppendLine("  toJSON() {");
        var collision = members.GroupBy(member => member.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
            _output.Append("    throw new TypeError(")
                .Append(JsonSerializer.Serialize($"{type} has more than one property named {collision.Key} in JSON."))
                .AppendLine(");");
        else
            _output.Append("    return { ")
                .Append(string.Join(", ", members.Select(member =>
                    $"{JavaScriptObjectKey(member.Name)}: {UserMemberAccess("this", member.Property)}")))
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
        if (IsUserException(type))
            EmitExceptionBaseCall(type, constructor);

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
            EmitStatements(constructor?.Body?.Statements ?? [], 2);
        _output.AppendLine("  }");
    }

    private void EmitExceptionBaseCall(INamedTypeSymbol type, ConstructorDeclarationSyntax? constructor)
    {
        var fallback = JsonSerializer.Serialize($"Exception of type '{type.ToDisplayString()}' was thrown.");
        var initializer = constructor?.Initializer;
        var message = fallback;
        string? inner = null;
        if (initializer is not null
            && _model.GetSymbolInfo(initializer).Symbol is IMethodSymbol baseConstructor)
            for (var index = 0; index < initializer.ArgumentList.Arguments.Count; index++)
            {
                var argument = initializer.ArgumentList.Arguments[index];
                var parameter = ArgumentParameter(baseConstructor, argument, index);
                if (parameter.Name == "message")
                    message = $"({Expression(argument.Expression)}) ?? {fallback}";
                else if (parameter.Name == "innerException")
                    inner = Expression(argument.Expression);
                else
                    throw UnsupportedSymbol(baseConstructor, initializer);
            }
        _output.Append("    super(").Append(message)
            .Append(inner is null ? "" : $", {{ cause: {inner} }}").AppendLine(");");
        _output.Append("    this.name = ").Append(JsonSerializer.Serialize(type.Name)).AppendLine(";");
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
                EmitStatements(getter.Body?.Statements ?? [], 2);
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
            EmitStatements(method.Body?.Statements ?? [], 2);
        _output.AppendLine("  }");
    }

    private static bool IsAutoProperty(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList is not null
        && property.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);

}
