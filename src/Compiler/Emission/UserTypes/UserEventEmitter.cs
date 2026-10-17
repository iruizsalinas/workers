using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private bool _usesDelegates;
    private readonly List<(INamedTypeSymbol Type, IMethodSymbol Method, IMethodSymbol Target)> _interfaceDelegateAliases = [];
    private string DelegateHelper(string name)
    {
        _usesDelegates = true;
        _helpers.Require(JavaScriptHelper.Delegates);
        return _helpers.Name(name);
    }

    private string DelegateCreation(IDelegateCreationOperation operation)
    {
        var value = Expression(operation.Target);
        var invoke = ((INamedTypeSymbol)ResolveType(operation.Type!)).DelegateInvokeMethod!;
        if (invoke.Parameters.Any(parameter => parameter.RefKind == RefKind.None && CopyValue(parameter.Type, "value") != "value"))
        {
            var copies = invoke.Parameters.Select((parameter, index) => parameter.RefKind == RefKind.None
                ? CopyValue(parameter.Type, $"args[{index}]") : $"args[{index}]");
            value = $"{DelegateHelper("delegateCopyArguments")}({value}, args => [{string.Join(", ", copies)}])";
        }
        var parent = operation.Parent;
        while (parent is IConversionOperation) parent = parent.Parent;
        // These adapters consume callbacks without exposing delegate values back
        // to C# code. Their inline callbacks need no multicast type metadata.
        if (parent is IArgumentOperation { Parameter.ContainingSymbol: IMethodSymbol method }
            && method.DeclaringSyntaxReferences.Length == 0
            && (method.ContainingType.ToDisplayString() == "System.Linq.Enumerable"
                || method.ContainingNamespace.ToDisplayString() is "Workers" or "Workers.Bindings")) return value;
        return $"{DelegateHelper("delegateTyped")}({value}, {System.Text.Json.JsonSerializer.Serialize(RuntimeTypeName(ResolveType(operation.Type!)))})";
    }

    private void EmitDelegateMethodAliases()
    {
        if (!_usesDelegates) return;
        var identity = _helpers.Name("delegateIdentity");
        foreach (var (type, method, target) in _interfaceDelegateAliases)
            _output.Append(_userTypes[type]).Append(".prototype.").Append(UserInstanceMethodName(method)).Append('[').Append(identity)
                .Append("] = receiver => ").Append(IsSourceInterface(target.ContainingType) ? _userMethods[target] : "receiver." + UserInstanceMethodName(target)).AppendLine(";");
        foreach (var (method, dispatcher) in _genericDispatchers)
        {
            _output.Append(dispatcher).Append('[').Append(identity).AppendLine("] = receiver => {");
            foreach (var (type, target) in _genericDispatchTargets[method].Where(pair => _runtimeInterfaceTypes.Contains(pair.Key)))
                _output.Append("  if (receiver instanceof ").Append(RuntimeInterfaceTypeName(type)).Append(") return ")
                    .Append(_userMethods[target]).AppendLine(";");
            _output.AppendLine("  switch (Object.getPrototypeOf(receiver)) {");
            foreach (var (type, target) in _genericDispatchTargets[method].Where(pair => !_runtimeInterfaceTypes.Contains(pair.Key)))
                _output.Append("    case ").Append(_userTypes[type]).Append(".prototype: return ").Append(_userMethods[target]).AppendLine(";");
            _output.AppendLine("  }").AppendLine("};");
        }
        EmitRuntimeInterfaceDelegateAliases(identity);
    }

    private string EventAccess(IEventReferenceOperation operation)
    {
        var symbol = (IEventSymbol)StaticInterfaceImplementation(operation.Event, StaticMemberConstraint(operation.Syntax), operation.Syntax);
        return symbol.IsStatic ? $"{StaticHolder(symbol.ContainingType)}(){StaticMemberKey(symbol)}"
            : UserMemberAccess(operation.Instance is null ? "this" : NeedsNullCheck(operation.Instance) ? NotNull(Operand(operation.Instance)) : Operand(operation.Instance), symbol);
    }

    private string EventAssignment(IEventAssignmentOperation operation)
    {
        var reference = (IEventReferenceOperation)operation.EventReference;
        var symbol = (IEventSymbol)StaticInterfaceImplementation(reference.Event, StaticMemberConstraint(reference.Syntax), reference.Syntax);
        var accessor = (operation.Adds ? symbol.AddMethod : symbol.RemoveMethod)!;
        var value = Expression(operation.HandlerValue);
        var receiver = symbol.IsStatic ? "null" : Expression(reference.Instance!);
        // Both field-like and custom events use accessor calls. Evaluate the handler
        // before the receiver null check, as for ordinary instance invocations.
        var call = symbol.IsStatic || IsSourceInterface(symbol.ContainingType) && !accessor.IsAbstract
            ? $"{QueueEventAccessor(accessor, operation.Syntax)}.call({(symbol.IsStatic ? "receiver" : NotNull("receiver"))}, handler)"
            : $"{NotNull("receiver")}.{UserInstanceMethodName(accessor)}(handler)";
        return $"((receiver, handler) => {call})({receiver}, {value})";
    }

    private string QueueEventAccessor(IMethodSymbol accessor, SyntaxNode source) => QueueSourceAccessor(accessor, source);

    private void EmitUserEvents(TypeDeclarationSyntax declaration)
    {
        foreach (var symbol in ((INamedTypeSymbol)_model.GetDeclaredSymbol(declaration)!).GetMembers().OfType<IEventSymbol>().Where(item => !item.IsStatic && !item.IsAbstract))
        {
            foreach (var accessor in new[] { symbol.AddMethod!, symbol.RemoveMethod! })
            {
                var value = UserIdentifier(accessor.Parameters[0], accessor.Parameters[0].Name);
                _output.Append("  ").Append(UserInstanceMethodName(accessor)).Append('(').Append(value).AppendLine(") {");
                EmitEventAccessorBody(accessor, declaration, 2);
                _output.AppendLine("  }");
            }
        }
    }

    private void EmitEventAccessorBody(IMethodSymbol method, SyntaxNode source, int depth)
    {
        var symbol = (IEventSymbol)method.AssociatedSymbol!;
        var value = UserIdentifier(method.Parameters[0], method.Parameters[0].Name);
        var syntax = symbol.DeclaringSyntaxReferences[0].GetSyntax();
        if (syntax is VariableDeclaratorSyntax)
        {
            var storage = symbol.IsStatic ? $"{StaticHolder(symbol.ContainingType)}(){StaticMemberKey(symbol)}" : UserMemberAccess("this", symbol);
            _output.Append(' ', depth * 2).Append(storage).Append(" = ")
                .Append(DelegateHelper(method.MethodKind == MethodKind.EventAdd ? "delegateCombine" : "delegateRemove"))
                .Append('(').Append(storage).Append(", ").Append(value).AppendLine(");");
        }
        else
        {
            var accessor = syntax as AccessorDeclarationSyntax
                ?? ((EventDeclarationSyntax)syntax).AccessorList!.Accessors.Single(item => item.Keyword.ValueText == (method.MethodKind == MethodKind.EventAdd ? "add" : "remove"));
            EmitWithExpressionScope(depth, () =>
            {
                if (accessor.ExpressionBody is not null) _output.Append(' ', depth * 2).Append(Expression(accessor.ExpressionBody.Expression)).AppendLine(";");
                else EmitStatements(accessor.Body?.Statements ?? [], depth);
            });
        }
    }
}
