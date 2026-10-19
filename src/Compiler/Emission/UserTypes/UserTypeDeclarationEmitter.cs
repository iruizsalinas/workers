using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private void EmitUserCode()
    {
        while (_pendingRecordHashes.Count != 0 || _pendingSourceAccessors.Count != 0 || _pendingUserMethods.Count != 0 || _pendingUserTypes.Count != 0
               || _pendingStaticHolders.Count != 0 || _pendingStaticGetters.Count != 0
               || _pendingRecordTextFunctions.Count != 0 || _pendingRecordPrintFunctions.Count != 0
               || _pendingRecordEqualityFunctions.Count != 0 || _pendingEnumTextFunctions.Count != 0)
        {
            EmitPendingEnumTextFunctions();
            EmitPendingRecordEqualityFunctions();
            EmitPendingRecordTextFunctions();
            EmitUserMethods();
            EmitSourceAccessors();
            EmitRecordHashes();
            EmitPendingStaticState();
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
        using var specialization = SpecializationScope(type);
        string? parentName = null;
        if (type.BaseType is { } parent && IsUserInstanceType(parent))
        {
            parentName = QueueUserType(parent, type.DeclaringSyntaxReferences[0].GetSyntax());
            if (_emittedUserTypes.Add(parent)) EmitUserType(parent);
        }
        if (type.DeclaringSyntaxReferences.Length != 1
            || type.DeclaringSyntaxReferences[0].GetSyntax() is not TypeDeclarationSyntax declaration)
            throw new NotSupportedException($"WRK119: User type '{type}' must have one class or record declaration.");
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        _diagnosticNode = declaration;
        ValidateUserType(type, declaration);

        _output.Append("class ").Append(_userTypes[type])
            .Append(parentName is not null ? " extends " + parentName : IsUserException(type) ? " extends Error" : "").AppendLine(" {");
        if (type.IsRecord && !RequiresUserClass(type))
            _output.Append("  get [").Append(PlainRecordIdentitySlot).Append("]() { return ").Append(PlainRecordIdentity(type)).AppendLine("; }");
        if (IsException(type))
            _output.Append("  get ").Append(ExceptionTypeNameSlot).Append("() { return ")
                .Append(JsonSerializer.Serialize(type.ToDisplayString())).AppendLine("; }");
        EmitUserConstructor(type, declaration);
        EmitUserRecordCopyConstructor(type, declaration);
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>().Where(property =>
                     !property.Modifiers.Any(SyntaxKind.StaticKeyword) && !property.Modifiers.Any(SyntaxKind.AbstractKeyword)))
        {
            if (!IsAutoProperty(property)) EmitComputedProperty(property);
            else if (_model.GetDeclaredSymbol(property) is IPropertySymbol { IsVirtual: true } or IPropertySymbol { IsOverride: true })
                EmitVirtualAutoProperty(property);
        }
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>().Where(method =>
                     _model.GetDeclaredSymbol(method) is IMethodSymbol { IsStatic: false, IsGenericMethod: false, IsAbstract: false }))
            EmitUserInstanceMethod(method);
        foreach (var indexer in declaration.Members.OfType<IndexerDeclarationSyntax>()) EmitUserIndexer(indexer);
        EmitUserEvents(declaration);
        EmitUserJsonProjection(type, declaration);
        EmitUserInterfaceMembers(type, declaration);
        EmitRecordHierarchyMembers(type, declaration);
        EmitUserObjectTextMember(type, declaration);
        _output.AppendLine("}").AppendLine();
    }

    // Workers APIs serialize with JSON.stringify, which calls toJSON. The names follow the web
    // contract (camelCase unless JsonPropertyName is present), matching plain records.
    private void EmitUserJsonProjection(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var members = JsonContractProperties(type)
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

    private void EmitComputedProperty(PropertyDeclarationSyntax property)
    {
        var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
        var name = UserMemberDeclarationName(symbol);
        foreach (var getter in new[] { true, false })
        {
            var accessorSymbol = getter ? symbol.GetMethod : symbol.SetMethod;
            if (accessorSymbol is null) continue;
            _output.Append(getter ? "  get " : "  set ").Append(name)
                .Append('(').Append(getter ? "" : UserIdentifier(accessorSymbol.Parameters[0], accessorSymbol.Parameters[0].Name)).AppendLine(") {");
            EmitWithExpressionScope(2, () =>
            {
                if (!getter) EmitSetterValueCopy(accessorSymbol, 2);
                var syntax = property.AccessorList?.Accessors.SingleOrDefault(accessor => getter
                    ? accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                    : accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
                var expression = getter ? property.ExpressionBody?.Expression ?? syntax?.ExpressionBody?.Expression : syntax?.ExpressionBody?.Expression;
                if (expression is not null)
                    _output.Append("    ").Append(getter ? "return " : "").Append(ValueExpression(expression)).AppendLine(";");
                else if (syntax is not null && IsAutoAccessor(syntax) && FieldKeywordBackingField(symbol) is { } backing)
                    _output.Append("    ").AppendLine(AutoAccessorStatement(getter, BackingStorage("this", backing),
                        getter ? "" : UserIdentifier(accessorSymbol.Parameters[0], accessorSymbol.Parameters[0].Name)));
                else
                    EmitStatements(syntax?.Body?.Statements ?? [], 2);
            });
            _output.AppendLine("  }");
        }
        EmitInheritedPropertyAccessors(symbol, property);
    }

    private void EmitUserInstanceMethod(MethodDeclarationSyntax method)
    {
        var symbol = (IMethodSymbol)_model.GetDeclaredSymbol(method)!;
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        var isIterator = IsIterator(method);
        var isEnumerable = IsEnumerableIterator(method, symbol);
        _output.Append("  ").Append(isAsync && !isEnumerable ? "async " : "").Append(isIterator && !isEnumerable ? "*" : "")
            .Append(UserInstanceMethodName(symbol)).Append('(')
            .Append(string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration))).AppendLine(") {");
        EmitWithExpressionScope(2, () =>
        {
            if (isEnumerable)
                EmitEnumerableIteratorBody(method, 2);
            else if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(ValueExpression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                EmitStatements(method.Body?.Statements ?? [], 2);
        });
        _output.AppendLine("  }");
    }

    private static bool IsAutoProperty(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList is not null
        && property.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);

}
