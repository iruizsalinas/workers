using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// C# 14 extension blocks. Each member is emitted as a static function whose first
// parameter is the block's receiver, mirroring the implementation Roslyn lowers it to.
// Like classic extension methods, a reference receiver is never null-checked.
internal sealed partial class JavaScriptEmitter
{
    private static bool IsExtensionBlockMember(ISymbol? symbol) => symbol?.ContainingType is { IsExtension: true };

    private static bool IsExtensionBlockProperty(IOperation operation, out IPropertyReferenceOperation property)
    {
        property = operation as IPropertyReferenceOperation ?? null!;
        return property is not null && IsExtensionBlockMember(property.Property);
    }

    // The class that declares the block; static state and initialization belong to it.
    private static INamedTypeSymbol DeclaringClass(ISymbol symbol) =>
        symbol.ContainingType.IsExtension ? symbol.ContainingType.ContainingType : symbol.ContainingType;

    // By-reference receivers (extension(ref Point point)) would need writable storage at each call site.
    private static void ValidateExtensionReceiver(IMethodSymbol member, SyntaxNode source)
    {
        if (!member.IsStatic && member.ContainingType.ExtensionParameter is not { RefKind: RefKind.None })
            throw UnsupportedSymbol(member, source);
    }

    // Read the receiver from the member's own extension block, so it shares the member's syntax tree.
    private static ParameterSyntax? ExtensionReceiverSyntax(IMethodSymbol member, SyntaxNode memberDeclaration, SyntaxNode source)
    {
        ValidateExtensionReceiver(member, source);
        if (member.IsStatic) return null;
        return memberDeclaration.Parent is ExtensionBlockDeclarationSyntax { ParameterList.Parameters: [{ } receiver] }
            ? receiver
            : throw UnsupportedSymbol(member, source);
    }

    // Calls written against the lowered implementation, such as Ext.Shout(text, 2), name the same
    // function: its parameters are the receiver followed by the member's own parameters.
    private IMethodSymbol? ExtensionBlockMemberFor(IMethodSymbol implementation)
    {
        if (!implementation.IsImplicitlyDeclared || IsExtensionBlockMember(implementation)
            || implementation.ContainingType is not { IsStatic: true } container
            || implementation.IsGenericMethod)
            return null;
        foreach (var block in container.GetTypeMembers().Where(type => type.IsExtension && type.Arity == 0))
            foreach (var member in block.GetMembers())
            {
                var methods = member switch
                {
                    IMethodSymbol method => new[] { method },
                    IPropertySymbol property => new[] { property.GetMethod, property.SetMethod }.OfType<IMethodSymbol>().ToArray(),
                    _ => []
                };
                foreach (var method in methods)
                    if (SymbolEqualityComparer.Default.Equals(method.AssociatedExtensionImplementation, implementation.OriginalDefinition))
                        return method;
            }
        return null;
    }

    private string EmitExtensionBlockInvocation(IInvocationOperation operation, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var function = QueueUserMethod(method, invocation);
        if (method.IsStatic)
            return EmitNormalizedInvocation(invocation, method, null,
                (_, values) => $"{function}({string.Join(", ", values)})");
        var instance = operation.Instance ?? throw UnsupportedSymbol(method, invocation);
        return EmitNormalizedInvocation(invocation, method, ValueExpression(instance),
            (target, values) => $"{function}({string.Join(", ", new[] { target! }.Concat(values))})");
    }

    private string ExtensionAccessorCall(IPropertySymbol property, bool getter, SyntaxNode source, string? receiver, string? value = null)
    {
        var accessor = (getter ? property.GetMethod : property.SetMethod) ?? throw UnsupportedSymbol(property, source);
        var arguments = new[] { receiver, value }.OfType<string>();
        return $"{QueueExtensionAccessor(accessor, source)}({string.Join(", ", arguments)})";
    }

    private string ExtensionPropertyGet(IPropertyReferenceOperation operation, string? receiverOverride = null)
    {
        var property = operation.Property;
        var receiver = property.IsStatic ? null
            : receiverOverride ?? ValueExpression(operation.Instance ?? throw UnsupportedSymbol(property, operation.Syntax));
        return ExtensionAccessorCall(property, true, operation.Syntax, receiver);
    }

    // A storage reference whose receiver is captured once, for writes and read-modify-write.
    private string ExtensionPropertyReference(IPropertyReferenceOperation operation)
    {
        var property = operation.Property;
        var receiver = property.IsStatic ? null : "receiver";
        var accessors = new List<string>();
        if (property.GetMethod is not null)
            accessors.Add($"get value() {{ return {ExtensionAccessorCall(property, true, operation.Syntax, receiver)}; }}");
        if (property.SetMethod is not null)
            accessors.Add($"set value(assigned) {{ {ExtensionAccessorCall(property, false, operation.Syntax, receiver, "assigned")}; }}");
        var reference = $"({{ {string.Join(", ", accessors)} }})";
        if (property.IsStatic) return reference;
        return $"((receiver) => {reference})({ValueExpression(operation.Instance ?? throw UnsupportedSymbol(property, operation.Syntax))})";
    }

    private string QueueExtensionAccessor(IMethodSymbol accessor, SyntaxNode source)
    {
        accessor = ResolveMethod(accessor);
        if (accessor.MethodKind is not (MethodKind.PropertyGet or MethodKind.PropertySet)
            || accessor.AssociatedSymbol is not IPropertySymbol { IsIndexer: false } property
            || property.DeclaringSyntaxReferences is not [{ } reference]
            || reference.GetSyntax() is not PropertyDeclarationSyntax)
            throw UnsupportedSymbol(accessor, source);
        ValidateExtensionReceiver(accessor, source);
        if (_userMethods.TryGetValue(accessor, out var existing)) return existing;
        CountSpecialization(accessor, source);
        var name = _names.ForMethod(accessor, _userMethods.Count);
        _userMethods.Add(accessor, name);
        _pendingUserMethods.Enqueue(accessor);
        return name;
    }

    private void EmitExtensionAccessor(IMethodSymbol accessor)
    {
        var property = (IPropertySymbol)accessor.AssociatedSymbol!;
        var declaration = (PropertyDeclarationSyntax)property.DeclaringSyntaxReferences.Single().GetSyntax();
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        _diagnosticNode = declaration;
        var getter = accessor.MethodKind == MethodKind.PropertyGet;
        var parameters = new List<string>();
        if (ExtensionReceiverSyntax(accessor, declaration, declaration) is { } receiver) parameters.Add(ParameterName(receiver));
        if (!getter) parameters.Add(UserIdentifier(accessor.Parameters.Last(), accessor.Parameters.Last().Name));
        _output.Append("function ").Append(_userMethods[accessor]).Append('(').Append(string.Join(", ", parameters)).AppendLine(") {");
        EmitWithExpressionScope(1, () =>
        {
            EmitExplicitStaticInitialization(DeclaringClass(accessor), 1);
            if (!getter) EmitSetterValueCopy(accessor, 1);
            var syntax = declaration.AccessorList?.Accessors.SingleOrDefault(item => getter
                ? item.IsKind(SyntaxKind.GetAccessorDeclaration)
                : item.IsKind(SyntaxKind.SetAccessorDeclaration) || item.IsKind(SyntaxKind.InitAccessorDeclaration));
            var expression = getter ? declaration.ExpressionBody?.Expression ?? syntax?.ExpressionBody?.Expression : syntax?.ExpressionBody?.Expression;
            if (expression is not null) _output.Append("  ").Append(getter ? "return " : "").Append(ValueExpression(expression)).AppendLine(";");
            else EmitStatements(syntax?.Body?.Statements ?? [], 1);
        });
        _output.AppendLine("}").AppendLine();
    }
}
