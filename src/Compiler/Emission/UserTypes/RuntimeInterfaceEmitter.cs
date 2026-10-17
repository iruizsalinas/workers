using Microsoft.CodeAnalysis;

internal sealed partial class JavaScriptEmitter
{
    private readonly HashSet<INamedTypeSymbol> _runtimeInterfaceTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, string> _runtimeInterfaceCalls = new(SymbolEqualityComparer.Default);
    private readonly Queue<(IMethodSymbol Method, SyntaxNode Source)> _pendingRuntimeInterfaceCalls = new();

    private string RuntimeInterfaceTypeName(INamedTypeSymbol type) =>
        type.BaseType?.ToDisplayString() is "Workers.WorkerEntrypoint" or "Workers.DurableObject"
            ? RuntimeClassName(type) : UserIdentifier(type, type.Name);

    private string? RuntimeInterfacePropertyReference(IPropertySymbol property, string receiver, SyntaxNode source)
    {
        if (!_runtimeInterfaceTypes.Any(type => _compilation.ClassifyConversion(type, property.ContainingType).IsImplicit)) return null;
        var accessors = new List<string>();
        if (property.GetMethod is { } getter)
            accessors.Add($"get value() {{ return {SourceInterfaceInvocation(getter, "receiver", [], source)}; }}");
        if (property.SetMethod is { } setter)
            accessors.Add($"set value(assigned) {{ {SourceInterfaceInvocation(setter, "receiver", ["assigned"], source)}; }}");
        return $"((receiver) => ({{ {string.Join(", ", accessors)} }}))({receiver}).value";
    }

    private string SourceInterfaceFallback(IMethodSymbol method, string receiver, string[] arguments) =>
        method.AssociatedSymbol is IPropertySymbol { IsIndexer: false } property
            ? method.MethodKind == MethodKind.PropertyGet ? UserMemberAccess(receiver, property)
                : $"({UserMemberAccess(receiver, property)} = {arguments[0]})"
            : $"{receiver}.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";

    private string SourceInterfaceInvocation(IMethodSymbol method, string receiver, string[] arguments, SyntaxNode source)
    {
        method = ResolveMethod(method);
        if (!_runtimeInterfaceTypes.Any(type => _compilation.ClassifyConversion(type, method.ContainingType).IsImplicit))
            return SourceInterfaceFallback(method, receiver, arguments);
        if (!_runtimeInterfaceCalls.TryGetValue(method, out var name))
        {
            name = _names.Get("runtime-interface-call:" + method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                "call$" + method.ContainingType.Name + "$" + method.Name);
            _runtimeInterfaceCalls.Add(method, name);
            _pendingRuntimeInterfaceCalls.Enqueue((method, source));
        }
        return $"{name}({string.Join(", ", new[] { receiver }.Concat(arguments))})";
    }

    private void EmitPendingRuntimeInterfaceCalls()
    {
        while (_pendingRuntimeInterfaceCalls.TryDequeue(out var pending))
        {
            var (method, source) = pending;
            using var specialization = SpecializationScope(method.ContainingType);
            var arguments = method.Parameters.Select((_, index) => "$arg" + index).ToArray();
            _output.Append("function ").Append(_runtimeInterfaceCalls[method]).Append("(receiver")
                .Append(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments)).AppendLine(") {");
            foreach (var type in _runtimeInterfaceTypes.Where(type =>
                         _compilation.ClassifyConversion(type, method.ContainingType).IsImplicit))
            {
                var implementation = FindInterfaceImplementation(type, method.AssociatedSymbol ?? method);
                var target = implementation is IPropertySymbol property
                    ? PropertyAccessor(property, method.MethodKind == MethodKind.PropertyGet)
                    : implementation as IMethodSymbol;
                if (target is null)
                    throw UnsupportedSymbol(method, source);
                var call = IsSourceInterface(target.ContainingType)
                    ? $"{(target.MethodKind == MethodKind.Ordinary ? QueueUserMethod(target, source) : QueueSourceAccessor(target, source))}.call(receiver{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})"
                    : $"receiver{(NeedsRpcResultWrapper(target) ? "[" + RpcMethodBodySlot(target) + "]" : "." + GeneratedInstanceMethodName(target))}({string.Join(", ", arguments)})";
                _output.Append("  if (receiver instanceof ").Append(RuntimeInterfaceTypeName(type)).Append(") return ").Append(call).AppendLine(";");
            }
            _output.Append("  return ").Append(SourceInterfaceFallback(method, "receiver", arguments)).AppendLine(";");
            _output.AppendLine("}").AppendLine();
        }
    }

    private void EmitRuntimeInterfaceDelegateAliases(string identity)
    {
        foreach (var (method, dispatcher) in _runtimeInterfaceCalls.Where(pair => pair.Key.MethodKind == MethodKind.Ordinary))
        {
            _output.Append(dispatcher).Append('[').Append(identity).AppendLine("] = receiver => {");
            foreach (var type in _runtimeInterfaceTypes.Where(type =>
                         _compilation.ClassifyConversion(type, method.ContainingType).IsImplicit))
            {
                var target = (IMethodSymbol)FindInterfaceImplementation(type, method)!;
                var reference = IsSourceInterface(target.ContainingType) ? _userMethods[target]
                    : $"receiver{(NeedsRpcResultWrapper(target) ? "[" + RpcMethodBodySlot(target) + "]" : "." + GeneratedInstanceMethodName(target))}";
                _output.Append("  if (receiver instanceof ").Append(RuntimeInterfaceTypeName(type)).Append(") return ")
                    .Append(reference).AppendLine(";");
            }
            _output.Append("  const method = receiver.").Append(UserInstanceMethodName(method)).AppendLine(";");
            _output.Append("  return method[").Append(identity).AppendLine("]?.(receiver) ?? method;");
            _output.AppendLine("};");
        }
    }

    private void EmitRuntimeInterfaceMarkers()
    {
        foreach (var type in _runtimeInterfaceTypes)
            foreach (var contract in _requestedInterfaces.ToArray().Where(contract =>
                         _compilation.ClassifyConversion(type, contract).IsImplicit))
                _output.Append("Object.defineProperty(").Append(RuntimeInterfaceTypeName(type)).Append(".prototype, ")
                    .Append(InterfaceMarker(contract)).AppendLine(", { value: true });");
    }
}
