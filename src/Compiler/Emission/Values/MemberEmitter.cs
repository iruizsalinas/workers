using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Member(MemberAccessExpressionSyntax member)
    {
        var symbol = _model.GetSymbolInfo(member).Symbol;
        var property = symbol as IPropertySymbol;
        if (property is { IsStatic: true, Name: "UtcNow" }
            && property.ContainingType.ToDisplayString() == "System.DateTimeOffset") return "new Date()";
        if (symbol is { IsStatic: true, Name: "UnixEpoch", ContainingType: { } epochType }
            && epochType.ToDisplayString() == "System.DateTimeOffset") return "new Date(0)";
        if (symbol is { IsStatic: true, ContainingType: { } timeSpanType }
            && timeSpanType.ToDisplayString() == "System.TimeSpan")
            return TimeSpanStaticMember(member, symbol.Name);
        if (property is { IsStatic: false, ContainingType: { } durationType }
            && durationType.ToDisplayString() == "System.TimeSpan")
            return TimeSpanMember(member, property.Name);
        if (property is { IsStatic: false, ContainingType: { } dateTimeType }
            && dateTimeType.ToDisplayString() is "System.DateTimeOffset" or "System.DateTime")
            return DateTimeMember(member, property);
        if (property is { IsStatic: false, Name: "ValueKind", ContainingType: { } jsonElementType }
            && jsonElementType.ToDisplayString() == "System.Text.Json.JsonElement")
            return HelperInvocation(JavaScriptHelper.JsonElementValueKind, [Expression(member.Expression)]);
        if (property is { ContainingType: { } cancellationType }
            && cancellationType.ToDisplayString() == "System.Threading.CancellationToken")
        {
            var token = property.IsStatic ? "null" : Expression(member.Expression);
            return property.Name switch
            {
                "None" => "null",
                "CanBeCanceled" => $"{token} != null",
                "IsCancellationRequested" => $"{token}?.aborted === true",
                _ => throw UnsupportedSymbol(property, member)
            };
        }
        if (property is { IsStatic: false, ContainingType: { } sourceType }
            && sourceType.ToDisplayString() == "System.Threading.CancellationTokenSource")
        {
            var source = Expression(member.Expression);
            return property.Name switch
            {
                "Token" => $"{source}.signal",
                "IsCancellationRequested" => $"{source}.signal.aborted",
                _ => throw UnsupportedSymbol(property, member)
            };
        }
        if (property?.ContainingType.ToDisplayString() == "System.Random" && property.Name == "Shared") return "Math";
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
        if (symbol is IFieldSymbol { IsStatic: true, Name: "Empty", ContainingType: { } guidType }
            && guidType.ToDisplayString() == "System.Guid")
            return "\"00000000-0000-0000-0000-000000000000\"";
        if (property is { IsStatic: true, Name: "InvariantCulture" }
            && property.ContainingType.ToDisplayString() == "System.Globalization.CultureInfo")
            return "\"invariant\"";
        if (symbol is IFieldSymbol { IsStatic: true, Name: var mathName, ContainingType: { } mathType }
            && mathType.ToDisplayString() is "System.Math" or "System.MathF"
            && mathName is "E" or "PI" or "Tau")
        {
            var value = mathName switch { "E" => "Math.E", "PI" => "Math.PI", _ => "Math.PI * 2" };
            return mathType.ToDisplayString() == "System.MathF" ? $"Math.fround({value})" : value;
        }
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
            && enumType.ToDisplayString() == "Workers.DigestAlgorithm")
            return field.Name switch
            {
                "Sha1" => "\"SHA-1\"",
                "Sha256" => "\"SHA-256\"",
                "Sha384" => "\"SHA-384\"",
                "Sha512" => "\"SHA-512\"",
                _ => throw Unsupported("WRK108", member)
            };
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
        if (property?.ContainingType.ToDisplayString() == "Workers.WebSocketPair") return $"{Expression(member.Expression)}[{(property.Name == "Client" ? 0 : 1)}]";
        if (property?.ContainingType.ToDisplayString() == "Workers.Body" && property.Name == "Empty") return "null";
        if (property?.ContainingType.ToDisplayString() == "Workers.Body" && property.Name == "IsEmpty")
            return $"{Expression(member.Expression)} === null";
        if (property?.ContainingType.ToDisplayString() == "Workers.TailEvent" && property.Name == "Events")
            return Expression(member.Expression);
        if (property?.ContainingType.ToDisplayString() == "Workers.TailRequest" && property.Name == "Headers")
            return $"new Headers({Expression(member.Expression)}.headers)";
        if (property?.ContainingType.ToDisplayString() == "Workers.ScheduledEvent")
            return property.Name switch
            {
                "Type" => "\"scheduled\"",
                "Schedule" => $"{Expression(member.Expression)}.scheduledTime",
                _ => $"{Expression(member.Expression)}.{LowerFirst(property.Name)}"
            };
        if (property?.ContainingType.ToDisplayString() == "Workers.FormEntry")
            return FormEntryMember(member, property);
        if (property?.ContainingType.ToDisplayString() == "Workers.FormFile")
            return $"{Expression(member.Expression)}.{property.Name switch { "FileName" => "name", "ContentType" => "type", "Body" => "", _ => LowerFirst(property.Name) }}".TrimEnd('.');
        if (property?.ContainingType.ToDisplayString() == "Workers.HtmlContentOptions") return property.Name == "Html" ? "{ html: true }" : "{ html: false }";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Path") return $"new URL({Expression(member.Expression)}.url).pathname";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "PathAndQuery")
            return $"(value => value.pathname + value.search)(new URL({Expression(member.Expression)}.url))";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "QueryParameters") return $"new URL({Expression(member.Expression)}.url).searchParams";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Url")
            return $"new URL({Expression(member.Expression)}.url)";
        if (property?.ContainingType.ToDisplayString() == "Workers.Url")
            return $"{Expression(member.Expression)}.{property.Name switch
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
            return $"Array.from({Expression(member.Expression)}).length";
        if (property is { Name: "Length" }
            && (_model.GetTypeInfo(member.Expression).Type?.SpecialType == SpecialType.System_String
                || _model.GetTypeInfo(member.Expression).Type is IArrayTypeSymbol
                || property.ContainingType.OriginalDefinition.ToDisplayString() is
                    "System.Memory<T>" or "System.ReadOnlyMemory<T>"))
            return $"{Expression(member.Expression)}.length";
        if (property?.ContainingType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return $"{Expression(member.Expression)}[{(property.Name == "Key" ? 0 : 1)}]";
        if (property is { Name: "Key" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.IGrouping<TKey, TElement>")
            return $"{Expression(member.Expression)}.key";
        if (property is { Name: "Count" }
            && property.ContainingType.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>")
            return $"{Expression(member.Expression)}.count";
        if (property?.ContainingType.ToDisplayString() == "Workers.Response" && property.Name == "IsSuccessStatusCode")
            return $"{Expression(member.Expression)}.ok";
        if (property?.ContainingType.ToDisplayString() == "Workers.WorkerEntrypoint")
            return $"{Expression(member.Expression)}.{(property.Name == "Environment" ? "env" : "ctx")}";
        if (property?.ContainingType.ToDisplayString() == "Workers.KvListResult" && property.Name == "ListComplete")
            return $"{Expression(member.Expression)}.list_complete";
        if (property?.ContainingType.ToDisplayString() == "Workers.DurableObjectStorage" && property.Name == "Kv")
            return $"{Expression(member.Expression)}.kv";
        if (property?.ContainingType.ToDisplayString() == "System.Exception" && property.Name == "Message")
            return $"{Expression(member.Expression)}.message";
        if (property?.ContainingType.ToDisplayString() == "System.Text.StringBuilder" && property.Name == "Length")
            return $"{Expression(member.Expression)}.length";
        if (property is { Name: "Count", ContainingType: { } queueBatch }
            && BindingIntrinsicRegistry.IsQueueMessageBatch(queueBatch))
            return $"{Expression(member.Expression)}.messages.length";
        if (property is { Name: "Count" } && IsDictionary(_model.GetTypeInfo(member.Expression).Type))
            return $"Object.keys({Expression(member.Expression)}).length";
        if (property is { Name: "Count" } && IsSet(_model.GetTypeInfo(member.Expression).Type))
            return $"{Expression(member.Expression)}.size";
        if (property is { Name: "Count", ContainingType: { } collection }
            && (collection.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>"
                || collection.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.Generic.IReadOnlyCollection<T>")))
            return $"{Expression(member.Expression)}.length";
        if (symbol?.ContainingType is { } userType && IsUserInstanceType(userType) && RequiresUserClass(userType))
        {
            QueueUserType(userType, member);
            if (symbol is IFieldSymbol or IPropertySymbol)
                return UserMemberAccess(Expression(member.Expression), symbol);
        }
        ThrowIfUnsupportedFrameworkMember(symbol, member);
        return $"{Expression(member.Expression)}.{LowerFirst(symbol?.Name ?? member.Name.Identifier.ValueText)}";
    }

}
