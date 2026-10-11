using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Member(IOperation operation)
    {
        var member = operation.Syntax;
        var symbol = operation is IPropertyReferenceOperation propertyReference ? (ISymbol)propertyReference.Property : ((IFieldReferenceOperation)operation).Field;
        var instance = operation is IPropertyReferenceOperation propertyInstance ? propertyInstance.Instance : ((IFieldReferenceOperation)operation).Instance;
        string Receiver() => instance is null ? "this" : Operand(instance);
        if (operation is IPropertyReferenceOperation { Property.IsIndexer: true } indexer) return ElementAccess(indexer);
        if (symbol is IPropertySymbol { IsStatic: true, Name: "Ordinal" or "OrdinalIgnoreCase", ContainingType: { } comparerType }
            && comparerType.ToDisplayString() == "System.StringComparer")
            return $"\"{symbol.Name}\"";
        if (IsUserStaticState(symbol))
            return StaticMemberAccess(symbol!, member);
        var property = symbol as IPropertySymbol;
        if (property is not null && NativeMember(instance, property) is { } native) return native;
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
        if (symbol is IFieldSymbol { ContainingType: { } d1SessionType } sessionMode
            && d1SessionType.ToDisplayString() == "Workers.D1SessionMode")
            return sessionMode.Name switch
            {
                "FirstPrimary" => "\"first-primary\"",
                "FirstUnconstrained" => "\"first-unconstrained\"",
                _ => throw Unsupported("WRK108", member)
            };
        if (symbol is IFieldSymbol { ContainingType: { } redirectType } redirect
            && redirectType.ToDisplayString() == "Workers.RedirectMode")
            return JsonSerializer.Serialize(LowerFirst(redirect.Name));
        if (symbol is IFieldSymbol { ContainingType: { } transportType } transport
            && transportType.ToDisplayString() == "Workers.TcpSecureTransport")
            return JsonSerializer.Serialize(transport.Name.ToLowerInvariant());
        if (symbol is IFieldSymbol { ContainingType: { } compressionType } compression
            && compressionType.ToDisplayString() == "Workers.CompressionFormat")
            return JsonSerializer.Serialize(compression.Name switch
            {
                "Gzip" => "gzip",
                "Deflate" => "deflate",
                "DeflateRaw" => "deflate-raw",
                _ => throw Unsupported("WRK108", member)
            });
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
        if (property?.ContainingType.ToDisplayString() == "Workers.Body" && property.Name == "Empty") return "null";
        if (property?.ContainingType.ToDisplayString() == "Workers.Body" && property.Name == "IsEmpty")
            return $"{Receiver()} === null";
        if (property?.ContainingType.ToDisplayString() == "Workers.TailRequest" && property.Name == "Headers")
            return $"new Headers({Receiver()}.headers)";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Signal")
            return $"{Receiver()}.signal";
        if (property?.ContainingType.ToDisplayString() == "Workers.FormEntry")
            return FormEntryMember(member, property, Receiver());
        if (property?.ContainingType.ToDisplayString() == "Workers.FormFile")
            return $"{Receiver()}.{property.Name switch { "FileName" => "name", "ContentType" => "type", "Body" => "", _ => LowerFirst(property.Name) }}".TrimEnd('.');
        if (property?.ContainingType.ToDisplayString() == "Workers.HtmlContentOptions") return property.Name == "Html" ? "{ html: true }" : "{ html: false }";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Path") return $"new URL({Receiver()}.url).pathname";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "PathAndQuery")
            return $"(value => value.pathname + value.search)(new URL({Receiver()}.url))";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "QueryParameters") return $"new URL({Receiver()}.url).searchParams";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Cookies")
            return HelperInvocation(JavaScriptHelper.RequestCookies, [Receiver()]);
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Url")
            return $"new URL({Receiver()}.url)";
        if (property?.ContainingType.ToDisplayString() == "Workers.Url")
            return $"{Receiver()}.{property.Name switch
            {
                "Hostname" => "hostname",
                "Username" => "username",
                "Path" => "pathname",
                "Query" => "search",
                "Fragment" => "hash",
                "QueryParameters" => "searchParams",
                _ => LowerFirst(property.Name)
            }}";
        if (property?.ContainingType.ToDisplayString() == "Workers.Headers" && property.Name == "Count")
            return $"Array.from({Receiver()}).length";
        if (property is { Name: "Length" }
            && (instance?.Type?.SpecialType == SpecialType.System_String
                || instance?.Type is IArrayTypeSymbol
                || property.ContainingType.OriginalDefinition.ToDisplayString() is
                    "System.Memory<T>" or "System.ReadOnlyMemory<T>"))
            return $"{Receiver()}.length";
        if (property?.ContainingType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return $"{Receiver()}[{(property.Name == "Key" ? 0 : 1)}]";
        // QueryParameter values only come from iterating URLSearchParams, which yields [name, value] pairs.
        if (property is { Name: "Name" or "Value", ContainingType: { } parameterType }
            && parameterType.ToDisplayString() == "Workers.QueryParameter")
            return $"{Receiver()}[{(property.Name == "Name" ? 0 : 1)}]";
        if (property is { Name: "Key" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.IGrouping<TKey, TElement>")
            return $"{Receiver()}.key";
        if (property is { Name: "Count" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>")
            return $"{Receiver()}.count";
        if (property?.ContainingType.ToDisplayString() == "Workers.HtmlElement" && property.Name == "NamespaceUri")
            return $"{Receiver()}.namespaceURI";
        if (property?.ContainingType.ToDisplayString() == "Workers.WebSocketMessage" && property.Name == "IsText")
            return $"(typeof {Receiver()} === \"string\")";
        // The runtime reports these instants as millisecond numbers; the API exposes them as dates.
        if (property is { Name: "ScheduledTime" or "EventTimestamp" or "Timestamp" }
            && property.ContainingType.ToDisplayString() is "Workers.ScheduledEvent" or "Workers.TailItem" or "Workers.TailLog" or "Workers.TailException")
            return $"(value => value == null ? null : new Date(value))({Receiver()}.{LowerFirst(property.Name)})";
        if (property?.ContainingType.ToDisplayString() == "Workers.WorkerEntrypoint")
            return $"{Receiver()}.{(property.Name == "Environment" ? "env" : "ctx")}";
        if (property?.ContainingType.ToDisplayString() == "Workers.KvListResult" && property.Name == "ListComplete")
            return $"{Receiver()}.list_complete";
        if (property?.ContainingType.ToDisplayString() is "Workers.D1ResultMetadata" or "Workers.D1Timings")
            return $"{Receiver()}.{SnakeCase(property.Name)}";
        if (property?.ContainingType.ToDisplayString() == "Workers.DurableObjectStorage" && property.Name == "Kv")
            return $"{Receiver()}.kv";
        if (property?.ContainingType.ToDisplayString() == "System.Exception" && property.Name == "Message")
            return $"{Receiver()}.message";
        if (property is { Name: "Count", ContainingType: { } queueBatch }
            && BindingIntrinsicRegistry.IsQueueMessageBatch(queueBatch))
            return $"{Receiver()}.messages.length";
        if (property is { Name: "Keys" or "Values" } && IsDictionary(instance?.Type))
            return $"Object.{(property.Name == "Keys" ? "keys" : "values")}({Receiver()})";
        if (property is { Name: "Count" } && IsDictionary(instance?.Type))
            return $"Object.keys({Receiver()}).length";
        if (property is { Name: "Count" } && IsSet(instance?.Type))
            return $"{Receiver()}.size";
        if (property is { Name: "Count", ContainingType: { } collection }
            && (collection.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>"
                || collection.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>")))
            return $"{Receiver()}.length";
        if (symbol?.ContainingType is { } userType && IsUserInstanceType(userType) && RequiresUserClass(userType))
        {
            QueueUserType(userType, member);
            if (symbol is IFieldSymbol or IPropertySymbol)
                return UserMemberAccess(Receiver(), symbol);
        }
        ThrowIfUnsupportedFoundationMember(symbol, member);
        return $"{Receiver()}.{LowerFirst(symbol!.Name)}";
    }

}
