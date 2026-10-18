using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Member(IOperation operation, string? receiverOverride = null)
    {
        var member = operation.Syntax;
        var symbol = operation is IPropertyReferenceOperation propertyReference ? (ISymbol)propertyReference.Property : ((IFieldReferenceOperation)operation).Field;
        var instance = operation is IPropertyReferenceOperation propertyInstance ? propertyInstance.Instance : ((IFieldReferenceOperation)operation).Instance;
        string Receiver() => receiverOverride ?? (instance is null ? "this" : NeedsNullCheck(instance) ? NotNull(Operand(instance)) : Operand(instance));
        if (symbol.ContainingType.IsAnonymousType)
        {
            var name = AnonymousPropertyName((IPropertySymbol)symbol);
            return IsJavaScriptPropertyIdentifier(name) ? $"{Receiver()}.{name}" : $"{Receiver()}[{JsonSerializer.Serialize(name)}]";
        }
        if (operation is IPropertyReferenceOperation { Property.IsIndexer: true } indexer) return ElementAccess(indexer);
        if (symbol is IPropertySymbol { IsStatic: true, Name: "Ordinal" or "OrdinalIgnoreCase", ContainingType: { } comparerType }
            && comparerType.ToDisplayString() == "System.StringComparer")
            return $"\"{symbol.Name}\"";
        symbol = StaticInterfaceImplementation(symbol, StaticMemberConstraint(member), member);
        if (IsUserStaticState(symbol))
            return StaticMemberAccess(symbol!, member);
        var property = symbol as IPropertySymbol;
        if ((property?.ContainingType.ToDisplayString() == "System.Type" || property?.ContainingType.ToDisplayString() == "System.Reflection.MemberInfo" && instance?.Type?.ToDisplayString() == "System.Type")
            && property.Name is "Name" or "Namespace" or "IsArray" or "IsValueType" or "IsEnum" or "IsInterface" or "IsGenericType")
            return $"{Receiver()}.{LowerFirst(property.Name)}";
        if (UsesKeyDictionary(instance?.Type))
        {
            if (property?.Name == "Count") return $"{Receiver()}.size";
            if (property?.Name is "Keys" or "Values") return $"{Receiver()}.{property.Name.ToLowerInvariant()}()";
        }
        if (property?.ContainingType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            if (property.Name == "HasValue") return $"({Receiver()} != null)";
            if (property.Name == "Value")
                return $"((value) => {{ if (value == null) throw {NewBuiltInException("System.InvalidOperationException", "Nullable object must have a value.")}; return {CopyValue(property.Type, "value")}; }})({Receiver()})";
        }
        if (symbol is IFieldSymbol tupleField && tupleField.ContainingType.IsTupleType)
            return TupleFieldAccess(Receiver(), tupleField, member);
        if (property is not null && IsIndexType(property.ContainingType))
            return property.Name switch { "Value" => $"{Receiver()}[0]", "IsFromEnd" => $"{Receiver()}[1]",
                "Start" => "[0, false]", "End" => "[0, true]", _ => throw UnsupportedSymbol(property, member) };
        if (property is not null && IsRangeType(property.ContainingType))
            return property.Name switch { "Start" => $"{Receiver()}[0]", "End" => $"{Receiver()}[1]",
                "All" => "[[0, false], [0, true]]", _ => throw UnsupportedSymbol(property, member) };
        if (property is not null && member is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
            && HasPropertyDescriptor(property))
            return BasePropertyGet(property, member);
        if (IsSourceInterface(symbol.ContainingType))
        {
            if (property is { IsStatic: false, DeclaredAccessibility: Accessibility.Private })
                return $"{StaticGetter(property)}.call({Receiver()})";
            if (property is { IsStatic: false }
                && RuntimeInterfacePropertyReference(property, Receiver(), member) is { } runtimeProperty)
                return runtimeProperty;
            if (!symbol.IsStatic) return UserMemberAccess(Receiver(), symbol);
            if (property is not null) throw UnsupportedSymbol(property, member);
        }
        if (property is not null && NativeMember(property, Receiver()) is { } native) return native;
        if (property?.ContainingType.ToDisplayString() == "Workers.CacheStorage" && property.Name == "Default")
            return "caches.default";
        if (symbol is IFieldSymbol { ContainingType: { } comparisonType, Name: "Ordinal" }
            && comparisonType.ToDisplayString() == "System.StringComparison")
            return "\"ordinal\"";
        if (symbol is IFieldSymbol { ContainingType: { } ignoreCaseType, Name: "OrdinalIgnoreCase" }
            && ignoreCaseType.ToDisplayString() == "System.StringComparison")
            return "\"ordinalIgnoreCase\"";
        if (symbol is IFieldSymbol { IsStatic: true, Name: "Empty", ContainingType.SpecialType: SpecialType.System_String })
            return "\"\"";
        if (symbol is IFieldSymbol { IsStatic: true, Name: var floatingName, ContainingType: { } floatingType }
            && floatingType.SpecialType is SpecialType.System_Single or SpecialType.System_Double
            && floatingName is "NaN" or "PositiveInfinity" or "NegativeInfinity")
            return floatingName switch
            {
                "NaN" => "NaN",
                "PositiveInfinity" => "Infinity",
                _ => "-Infinity"
            };
        if (symbol is IFieldSymbol { ContainingType: { } enumType } field
            && CryptoEnumValue(enumType.ToDisplayString(), field.Name) is { } cryptoEnum)
            return cryptoEnum;
        if (symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } nativeField
            && NativeEnumValue(nativeField.ContainingType.ToDisplayString(), nativeField.Name) is { } nativeEnum)
            return JsonSerializer.Serialize(nativeEnum);
        if (symbol is IFieldSymbol { ContainingType: { } splitOptionsType } splitOptions
            && splitOptionsType.ToDisplayString() == "System.StringSplitOptions")
            return splitOptions.Name switch
            {
                "None" => "0",
                "RemoveEmptyEntries" => "1",
                "TrimEntries" => "2",
                _ => throw Unsupported("WRK108", member)
            };
        if (symbol is IFieldSymbol { IsStatic: true, HasConstantValue: true } constant)
            return LiteralConstant(constant.ConstantValue, member);
        if (symbol is IFieldSymbol { IsStatic: true })
            throw Unsupported("WRK110", member);
        if (property is { Name: "CompletedTask", ContainingType: { } taskType }
            && taskType.ToDisplayString() is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask")
            return "Promise.resolve()";
        if (property?.ContainingType.ToDisplayString() == "Workers.WebSocketPair") return $"{Receiver()}[{(property.Name == "Client" ? 0 : 1)}]";
        if (property?.ContainingType.ToDisplayString() == "Workers.TraceItemFetchEventInfoRequest" && property.Name == "Headers")
            return $"new Headers({Receiver()}.headers)";
        // Only fetch invocations carry a request; other event types are exposed as null.
        if (property?.ContainingType.ToDisplayString() == "Workers.TraceItem" && property.Name == "Event")
            return $"(value => value != null && \"request\" in value ? value : null)({Receiver()}.event)";
        if (property?.ContainingType.ToDisplayString() == "Workers.FormDataEntryValue")
            return FormEntryMember(member, property, Receiver());
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Cookies")
            return HelperInvocation(JavaScriptHelper.RequestCookies, [Receiver()]);
        if (property?.ContainingType.ToDisplayString() == "Workers.Response" && property.Name == "Cookies")
            return $"{Receiver()}.headers";
        if (property?.ContainingType.ToDisplayString() is "Workers.WorkerEntrypoint" or "Workers.DurableObject")
            return $"{Receiver()}.{LowerFirst(property.Name)}";
        if (property is { Name: "Body", ContainingType: { } messageType }
            && messageType.OriginalDefinition.ToDisplayString() == "Workers.Message<T>")
            return JsonBoundaryConverter(messageType.TypeArguments[0], member, JsonWebMode) is { } convertBody
                ? $"{RequireHelperName(JavaScriptHelper.QueueMessages, "queueBody")}({Receiver()}, {convertBody})"
                : $"{Receiver()}.body";
        if (property is { Name: "Length" }
            && (instance?.Type?.SpecialType == SpecialType.System_String
                || instance?.Type is IArrayTypeSymbol
                || property.ContainingType.OriginalDefinition.ToDisplayString() is
                    "System.Memory<T>" or "System.ReadOnlyMemory<T>"))
            return $"{Receiver()}.length";
        if (property?.ContainingType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return $"{Receiver()}[{(property.Name == "Key" ? 0 : 1)}]";
        if (property is { Name: "Key" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.IGrouping<TKey, TElement>")
            return $"{Receiver()}.key";
        if (property is { Name: "Count" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>")
            return $"{Receiver()}.count";
        if (property?.ContainingType.ToDisplayString() == "Workers.WebSocketMessage" && property.Name == "IsText")
            return $"(typeof {Receiver()} === \"string\")";
        // The runtime reports these instants as millisecond numbers; the API exposes them as dates.
        if (property is { Name: "ScheduledTime" or "EventTimestamp" or "Timestamp" }
            && property.ContainingType.ToDisplayString() is "Workers.ScheduledController" or "Workers.AlarmInvocationInfo"
                or "Workers.TraceItem" or "Workers.TraceLog" or "Workers.TraceException" or "Workers.WorkflowInstanceEvent")
            return $"(value => value == null ? null : new Date(value))({Receiver()}.{LowerFirst(property.Name)})";
        if (property is not null && NativePropertyNames.Get(property.ContainingType.ToDisplayString(), property.Name) is { } nativeName)
            return $"{Receiver()}.{nativeName}";
        if (property?.ContainingType.ToDisplayString() == "System.Exception" && property.Name == "Message")
            return $"{Receiver()}.message";
        if (property is not null && IsException(property.ContainingType))
        {
            if (property.Name == "Message") return $"{Receiver()}.message";
            if (property.Name == "InnerException") return $"({Receiver()}.cause ?? null)";
            if (property.Name == "ParamName") return $"({Receiver()}.paramName ?? null)";
            if (property.Name == "ActualValue") return $"({Receiver()}.actualValue ?? null)";
        }
        if (property is { Name: "Keys" or "Values" } && IsDictionary(instance?.Type))
            return $"Object.{(property.Name == "Keys" ? "keys" : "values")}({Receiver()})";
        if (property is { Name: "Count" } && IsDictionary(instance?.Type))
            return $"Object.keys({Receiver()}).length";
        if (property is { Name: "Count" } && IsSet(instance?.Type))
            return $"{Receiver()}.size";
        if (property is { Name: "Count", ContainingType: { } collection }
            && (collection.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>"
                || collection.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>")))
            return CollectionCount(collection, instance?.Type, Receiver());
        if (symbol?.ContainingType is { } userType && IsUserInstanceType(userType) && RequiresUserClass(userType))
        {
            QueueUserType(userType, member);
            if (symbol is IFieldSymbol or IPropertySymbol)
                return UserMemberAccess(Receiver(), symbol);
        }
        if (symbol is IFieldSymbol generatedField && IsGeneratedInstanceType(generatedField.ContainingType))
            return $"{Receiver()}.{GeneratedFieldName(generatedField)}";
        ThrowIfUnsupportedFoundationMember(symbol, member);
        return $"{Receiver()}.{LowerFirst(symbol!.Name)}";
    }

    private static string CollectionCount(INamedTypeSymbol collection, ITypeSymbol? instanceType, string receiver) =>
        collection.TypeKind == TypeKind.Interface && !IsArrayBackedList(instanceType)
            ? $"(collection => Array.isArray(collection) || collection instanceof Uint8Array ? collection.length : collection instanceof Set || collection instanceof Map ? collection.size : typeof collection[Symbol.iterator] === \"function\" ? collection.length : Object.keys(collection).length)({receiver})"
            : $"{receiver}.length";

    private readonly Dictionary<(string Type, string Name), string> _anonymousPropertyNames = new();

    private string AnonymousPropertyName(IPropertySymbol property)
    {
        // Roslyn can expose different declaration symbols for the same anonymous type.
        // Use one storage name per shape, including inferred and explicit initializers.
        var key = (property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), property.Name);
        if (_anonymousPropertyNames.TryGetValue(key, out var existing)) return existing;
        var inferred = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is
            AnonymousObjectMemberDeclaratorSyntax { NameEquals: null, Expression: MemberAccessExpressionSyntax };
        var collision = property.ContainingType.GetMembers().OfType<IPropertySymbol>()
            .Any(other => other.Name != property.Name && LowerFirst(other.Name) == LowerFirst(property.Name));
        var name = inferred && !collision ? LowerFirst(property.Name) : property.Name;
        _anonymousPropertyNames.Add(key, name);
        return name;
    }

}
