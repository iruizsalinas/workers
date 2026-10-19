using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string DefaultFieldValue(ITypeSymbol type, SyntaxNode source)
    {
        type = ResolveType(type);
        if (IsIndexType(type)) return "[0, false]";
        if (IsRangeType(type)) return "[[0, false], [0, false]]";
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            return TupleValue(tuple.TupleElements.Select(element => DefaultFieldValue(element.Type, source)));
        if (type is INamedTypeSymbol { IsRecord: true, IsValueType: true } record
            && IsUserInstanceType(record) && !RequiresUserClass(record))
        {
            var members = record.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic)
                .Select(property => $"{JavaScriptObjectKey(UserMemberName(property))}: {DefaultFieldValue(property.Type, source)}");
            return $"Object.defineProperty({{ {string.Join(", ", members)} }}, {PlainRecordIdentitySlot}, {{ value: {PlainRecordIdentity(record)} }})";
        }
        if (type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return "null";
        if (type.SpecialType == SpecialType.System_Boolean) return "false";
        if (type.SpecialType == SpecialType.System_Char) return "\"\\u0000\"";
        if (type.TypeKind == TypeKind.Enum || type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return "0";
        if (type is INamedTypeSymbol named)
        {
            var definition = named.OriginalDefinition.ToDisplayString();
            if (named.TypeArguments is [{ SpecialType: SpecialType.System_Byte }]
                && definition is "System.Memory<T>" or "System.ReadOnlyMemory<T>" or "System.Span<T>" or "System.ReadOnlySpan<T>")
                return "new Uint8Array(0)";
            if (definition == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
                return $"[{DefaultFieldValue(named.TypeArguments[0], source)}, {DefaultFieldValue(named.TypeArguments[1], source)}]";
            if (definition == "System.Threading.Tasks.ValueTask<TResult>")
                return $"Promise.resolve({DefaultFieldValue(named.TypeArguments[0], source)})";
            if (definition == "System.Threading.Tasks.ValueTask") return "Promise.resolve()";
        }
        throw Unsupported("WRK108", source);
    }

    private void EmitHandler(string eventName, MethodDeclarationSyntax method)
    {
        _model = _compilation.GetSemanticModel(method.SyntaxTree);
        _diagnosticNode = method;
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterName));
        var isAsync = method.Modifiers.Any(token => token.RawKind == (int)SyntaxKind.AsyncKeyword);
        var type = _model.GetDeclaredSymbol(method)!.ContainingType;
        var asyncWrapper = isAsync && HasExplicitStaticConstructor(type);
        _output.Append(isAsync && !asyncWrapper ? "async " : "").Append("function ").Append(EventName(eventName)).Append('(').Append(parameters).AppendLine(") {");
        EmitWithExpressionScope(1, () =>
        {
            EmitExplicitStaticInitialization(type, 1);
            if (asyncWrapper) _output.AppendLine("  return (async () => {");
            if (method.ExpressionBody is not null)
                _output.Append("  return ").Append(ValueExpression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                EmitStatements(method.Body?.Statements ?? [], 1);
            if (asyncWrapper) _output.AppendLine("  })();");
        });
        _output.AppendLine("}").AppendLine();
    }

    private string QueueUserMethod(IMethodSymbol method, SyntaxNode callSite)
    {
        method = ResolveMethod(method);
        if (ExtensionBlockMemberFor(method) is { } extensionMember)
        {
            if (extensionMember.MethodKind != MethodKind.Ordinary) return QueueExtensionAccessor(extensionMember, callSite);
            method = extensionMember;
        }
        if (IsExtensionBlockMember(method)) ValidateExtensionReceiver(method, callSite);
        if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation) || method.IsAbstract
            || method.ReturnsByRef || method.ReturnsByRefReadonly
            || method.Parameters.Any(parameter => parameter.RefKind is not (RefKind.None or RefKind.Out)
                || parameter.IsParams && parameter.Type is not IArrayTypeSymbol)
            || method.DeclaringSyntaxReferences.Length != 1
            || method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax)
            throw UnsupportedSymbol(method, callSite);
        if (_userMethods.TryGetValue(method, out var existing))
            return existing;
        CountSpecialization(method, callSite);
        var name = _names.ForMethod(method, _userMethods.Count);
        _userMethods.Add(method, name);
        _pendingUserMethods.Enqueue(method);
        return name;
    }

    private string EmitUserInvocation(IMethodSymbol method, InvocationExpressionSyntax invocation, IReadOnlyList<string> arguments)
    {
        return $"{QueueUserMethod(method, invocation)}({string.Join(", ", arguments)})";
    }

    private void EmitUserMethods()
    {
        while (_pendingUserMethods.Count != 0)
        {
            var symbol = _pendingUserMethods.Dequeue();
            if (!_emittedUserMethods.Add(symbol)) continue;
            using var specialization = SpecializationScope(symbol.ContainingType, symbol);
            if (symbol.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
            {
                EmitExtensionAccessor(symbol);
                continue;
            }
            var declaration = (MethodDeclarationSyntax)symbol.DeclaringSyntaxReferences.Single().GetSyntax();
            _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
            _diagnosticNode = declaration;
            var extensionBlock = IsExtensionBlockMember(symbol);
            var receiver = extensionBlock ? ExtensionReceiverSyntax(symbol, declaration, declaration) : null;
            // Prepend explicitly: a collection expression typed as SeparatedSyntaxList would rebuild detached nodes.
            var parameterList = (receiver is null ? declaration.ParameterList.Parameters : declaration.ParameterList.Parameters.Prepend(receiver)).ToArray();
            var parameters = string.Join(", ", parameterList.Select(ParameterDeclaration));
            var isAsync = declaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(declaration);
            var isEnumerable = IsEnumerableIterator(declaration, symbol);
            var staticOwner = DeclaringClass(symbol);
            var asyncWrapper = isAsync && !isEnumerable && HasExplicitStaticConstructor(staticOwner);
            _output.Append(isAsync && !isEnumerable && !asyncWrapper ? "async " : "").Append(isIterator && !isEnumerable ? "function* " : "function ")
                .Append(_userMethods[symbol]).Append('(').Append(parameters).AppendLine(") {");
            if (!symbol.IsStatic && !extensionBlock)
                _output.AppendLine($"  if (this == null) throw {NullReferenceException()};");
            EmitWithExpressionScope(1, () =>
            {
                EmitExplicitStaticInitialization(staticOwner, 1);
                if (asyncWrapper) _output.AppendLine("  return (async () => {");
                if (isEnumerable)
                    EmitEnumerableIteratorBody(declaration, 1, parameterList);
                else if (declaration.ExpressionBody is not null)
                    _output.Append("  return ").Append(ValueExpression(declaration.ExpressionBody.Expression)).AppendLine(";");
                else
                    EmitStatements(declaration.Body?.Statements ?? [], 1);
                if (asyncWrapper) _output.AppendLine("  })();");
            });
            _output.AppendLine("}").AppendLine();
        }
    }

    private static bool IsEnumerableIterator(MethodDeclarationSyntax declaration, IMethodSymbol symbol) =>
        IsIterator(declaration) && symbol.ReturnType.OriginalDefinition.ToDisplayString() is
            "System.Collections.Generic.IEnumerable<T>" or "System.Collections.IEnumerable"
            or "System.Collections.Generic.IAsyncEnumerable<T>";

    // Each enumeration gets new parameter locals, while the factory captures the original
    // arguments and instance. A generator object alone can only be consumed once.
    private void EmitEnumerableIteratorBody(MethodDeclarationSyntax declaration, int depth, IReadOnlyList<ParameterSyntax>? parameterList = null)
    {
        var indent = new string(' ', depth * 2);
        var isAsync = declaration.Modifiers.Any(SyntaxKind.AsyncKeyword);
        parameterList ??= declaration.ParameterList.Parameters;
        var parameters = string.Join(", ", parameterList.Select(ParameterName));
        _output.Append(indent).Append("return { [Symbol.").Append(isAsync ? "asyncIterator" : "iterator")
            .Append("]: (").Append(isAsync ? "async " : "").Append("function* (").Append(parameters).AppendLine(") {");
        EmitWithExpressionScope(depth + 1, () =>
        {
            EmitIteratorParameterCopies(parameterList, depth + 1);
            EmitStatements(declaration.Body?.Statements ?? [], depth + 1);
        });
        _output.Append(indent).Append("}).bind(this").Append(parameters.Length == 0 ? "" : ", " + parameters).AppendLine(") };");
    }

    private void EmitIteratorParameterCopies(IEnumerable<ParameterSyntax> parameters, int depth)
    {
        foreach (var parameter in parameters)
        {
            var symbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
            var name = ParameterName(parameter);
            var copy = CopyValue(symbol.Type, name);
            if (copy != name)
                _output.Append(' ', depth * 2).Append(name).Append(" = ").Append(copy).AppendLine(";");
        }
    }

}
