using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<ITypeSymbol, string> _rpcReaders = new(SymbolEqualityComparer.Default);
    private readonly Queue<(ITypeSymbol Type, SyntaxNode Source)> _pendingRpcReaders = new();

    private string RpcCollectionKind(ITypeSymbol type) => type is IArrayTypeSymbol array
        ? array.ElementType.SpecialType == SpecialType.System_Byte ? "bytes" : "array"
        : type is INamedTypeSymbol { IsTupleType: true } ? "tuple"
        : type is INamedTypeSymbol set && IsSet(set) && IsCompositeKey(set.TypeArguments[0]) ? "keySet"
        : UsesKeyDictionary(type) ? "keyDictionary"
        : type.OriginalDefinition.ToDisplayString() switch
        {
            "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>"
                or "System.Collections.Generic.IReadOnlyList<T>" => "list",
            "System.Collections.Generic.Queue<T>" or "System.Collections.Generic.Stack<T>" => "array",
            "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>" => "collection",
            "System.Collections.Generic.HashSet<T>" or "System.Collections.Generic.ISet<T>"
                or "System.Collections.Generic.IReadOnlySet<T>" => "set",
            "System.Collections.Generic.IEnumerable<T>" => "enumerable",
            "System.Collections.Generic.KeyValuePair<TKey, TValue>" => "tuple",
            "System.Collections.Generic.Dictionary<TKey, TValue>" or "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                when type is INamedTypeSymbol { TypeArguments: [{ SpecialType: SpecialType.System_String }, _] } => "dictionary",
            "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>" => "promise",
            _ => ""
        };

    private bool RpcArgumentNeedsHydration(ITypeSymbol type)
    {
        type = UnwrapNullable(type);
        if (type is INamedTypeSymbol user && IsUserInstanceType(user)) return true;
        var kind = RpcCollectionKind(type);
        if (kind is "bytes" or "list" or "set" or "keySet" or "keyDictionary") return true;
        if (kind.Length == 0) return false;
        return type is IArrayTypeSymbol array ? RpcArgumentNeedsHydration(array.ElementType)
            : ((INamedTypeSymbol)type).TypeArguments.Any(RpcArgumentNeedsHydration);
    }

    private string? RpcArgumentReader(ITypeSymbol type, SyntaxNode source)
    {
        type = UnwrapNullable(ResolveType(type));
        if (!RpcArgumentNeedsHydration(type)) return null;
        if (_rpcReaders.TryGetValue(type, out var existing)) return existing;
        var name = _names.Get("rpc-reader:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "rpcRead");
        _rpcReaders.Add(type, name);
        _pendingRpcReaders.Enqueue((type, source));
        if (type is INamedTypeSymbol user && IsUserInstanceType(user)) QueueUserType(user, source);
        return name;
    }

    // RPC transmits the instance's own data. Restore its C# prototype in place, preserving
    // native clone aliases and cycles without invoking its constructor a second time.
    private void EmitPendingRpcReaders()
    {
        while (_pendingRpcReaders.TryDequeue(out var pending))
        {
            var (type, source) = pending;
            using var specialization = type is INamedTypeSymbol named ? SpecializationScope(named) : null;
            string Read(ITypeSymbol itemType, string value) => RpcArgumentReader(itemType, source) is { } reader
                ? $"{reader}({value}, seen)" : value;
            var kind = RpcCollectionKind(type);
            _output.Append("function ").Append(_rpcReaders[type]).AppendLine("(value, seen) {");
            _output.AppendLine(kind == "promise"
                ? "  if (value == null || typeof value.then !== \"function\") return value;"
                : "  if (value == null || typeof value !== \"object\") return value;");
            // One cloned object may be reached through different declared types.
            // Break cycles per reader so a base-type visit cannot skip derived state.
            _output.Append("  let visited = seen.get(").Append(_rpcReaders[type]).AppendLine(");");
            _output.AppendLine("  if (visited?.has(value)) return seen.get(value);");
            _output.Append("  if (visited === undefined) seen.set(").Append(_rpcReaders[type]).AppendLine(", visited = new WeakSet());");
            _output.AppendLine("  visited.add(value);");
            if (kind == "bytes")
            {
                _output.AppendLine("  const result = value instanceof Uint8Array ? value : new Uint8Array(value);");
                _output.AppendLine("  seen.set(value, result);");
                _output.AppendLine("  return result;");
            }
            else if (kind == "promise")
            {
                var item = ((INamedTypeSymbol)type).TypeArguments[0];
                _output.Append("  const result = value.then(item => ").Append(Read(item, "item")).AppendLine(");");
                _output.AppendLine("  seen.set(value, result);");
                _output.AppendLine("  return result;");
            }
            else if (kind is "keySet" or "keyDictionary")
            {
                var collection = (INamedTypeSymbol)type;
                var set = kind == "keySet";
                _output.AppendLine("  const items = Array.from(value);");
                _output.Append("  const result = ").Append(KeyCollection(collection, source)).AppendLine(";");
                // Keep the cloned container itself so erased references and forward aliases
                // still point to the same object after its C# collection behavior is restored.
                _output.AppendLine("  if (value instanceof Map) Map.prototype.clear.call(value);");
                _output.AppendLine("  else if (value instanceof Set) Set.prototype.clear.call(value);");
                _output.AppendLine("  Object.defineProperties(value, Object.getOwnPropertyDescriptors(result));");
                _output.Append("  ").Append(_helpers.Name("keyCollectionKinds")).Append(".set(value, ").Append(set ? "true" : "false").AppendLine(");");
                _output.AppendLine("  seen.set(value, value);");
                _output.AppendLine("  for (const item of items) {");
                _output.Append("    value.add(").Append(Read(collection.TypeArguments[0], set ? "item" : "item[0]"));
                if (!set) _output.Append(", ").Append(Read(collection.TypeArguments[1], "item[1]"));
                _output.AppendLine(");");
                _output.AppendLine("  }");
                _output.AppendLine("  return value;");
            }
            else
            {
                _output.AppendLine("  seen.set(value, value);");
                if (type is INamedTypeSymbol user && IsUserInstanceType(user))
                {
                    var className = QueueUserType(user, source);
                    _output.Append("  if (!(value instanceof ").Append(className).Append(")) Object.setPrototypeOf(value, ")
                        .Append(className).AppendLine(".prototype);");
                    for (var current = user; current is not null && IsUserInstanceType(current); current = current.BaseType)
                    {
                        foreach (var member in RecordEqualityMembers(current))
                        {
                            var storage = member.Symbol is IPropertySymbol property
                                ? UserPropertyStorage("value", property) : UserMemberAccess("value", member.Symbol);
                            var key = member.Symbol is IPropertySymbol { IsVirtual: true } or IPropertySymbol { IsOverride: true }
                                ? storage["value.".Length..] : UserMemberName(member.Symbol);
                            EmitRpcMemberRead(member.Type, key, storage, source);
                        }
                        if (current.DeclaringSyntaxReferences.Single().GetSyntax() is ClassDeclarationSyntax { ParameterList: { } parameters })
                            foreach (var syntax in parameters.Parameters)
                            {
                                var parameter = (IParameterSymbol)_compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax)!;
                                var storage = PrimaryParameterStorage(parameter, "value");
                                EmitRpcMemberRead(parameter.Type, storage["value.".Length..], storage, source);
                            }
                    }
                }
                else if (kind == "dictionary")
                {
                    var item = ((INamedTypeSymbol)type).TypeArguments[1];
                    _output.Append("  for (const key of Object.keys(value)) value[key] = ").Append(Read(item, "value[key]")).AppendLine(";");
                }
                else if (kind == "tuple")
                {
                    var tuple = (INamedTypeSymbol)type;
                    var items = tuple.IsTupleType ? tuple.TupleElements.Select(field => field.Type).ToArray() : tuple.TypeArguments.ToArray();
                    for (var index = 0; index < items.Length; index++)
                        _output.Append("  value[").Append(index).Append("] = ").Append(Read(items[index], $"value[{index}]")).AppendLine(";");
                    if (tuple.IsTupleType) _output.Append("  ").Append(_helpers.Require(JavaScriptHelper.Tuple)).AppendLine("(value);");
                }
                else
                {
                    var item = type is IArrayTypeSymbol array ? array.ElementType : ((INamedTypeSymbol)type).TypeArguments[0];
                    if (kind is "set" or "enumerable" or "collection")
                    {
                        _output.AppendLine("  if (value instanceof Set) {");
                        _output.Append("    const items = Array.from(value, item => ").Append(Read(item, "item")).AppendLine(");");
                        _output.AppendLine("    value.clear();");
                        _output.AppendLine("    for (const item of items) value.add(item);");
                        if (kind is "enumerable" or "collection" && item is INamedTypeSymbol pair && IsKeyValuePair(pair))
                        {
                            _output.AppendLine("  } else if (value instanceof Map) {");
                            _output.Append("    const items = Array.from(value, item => ").Append(Read(item, "item")).AppendLine(");");
                            _output.AppendLine("    value.clear();");
                            _output.AppendLine("    for (const [key, item] of items) value.set(key, item);");
                            _output.AppendLine("  } else if (!Array.isArray(value)) {");
                            _output.AppendLine("    for (const key of Object.keys(value)) {");
                            _output.Append("      const pair = ").Append(Read(item, "[key, value[key]]")).AppendLine(";");
                            _output.AppendLine("      value[key] = pair[1];");
                            _output.AppendLine("    }");
                        }
                        _output.AppendLine("  } else {");
                    }
                    _output.Append("  for (let index = 0; index < value.length; index++) value[index] = ").Append(Read(item, "value[index]")).AppendLine(";");
                    if (kind is "set" or "enumerable" or "collection") _output.AppendLine("  }");
                    if (kind == "list") _output.Append("  ").Append(_helpers.Require(JavaScriptHelper.ListState)).AppendLine("(value);");
                    if (kind == "set") _output.Append("  ").Append(_helpers.Require(JavaScriptHelper.SetState)).AppendLine("(value);");
                }
                _output.AppendLine("  return value;");
            }
            _output.AppendLine("}").AppendLine();
        }
    }

    private void EmitRpcMemberRead(ITypeSymbol type, string key, string storage, SyntaxNode source)
    {
        if (RpcArgumentReader(type, source) is not { } reader) return;
        _output.Append("  if (Object.hasOwn(value, ").Append(JsonSerializer.Serialize(key)).Append(")) ")
            .Append(storage).Append(" = ").Append(reader).Append('(').Append(storage).AppendLine(", seen);");
    }
}
