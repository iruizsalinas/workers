using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string EmitBindingIntrinsic(
        string receiver,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        BindingIntrinsic intrinsic) =>
        MaterializeBindingResult(invocation, method, EmitBindingIntrinsicCore(receiver, invocation, method, intrinsic));

    private string RequireHelperName(JavaScriptHelper helper, string name)
    {
        _helpers.Require(helper);
        return _helpers.Name(name);
    }

    // Values read through Workers APIs arrive as parsed JSON, SQLite rows or structured clones.
    // Typed reads hydrate user classes and records. Application code validates external input.
    private string MaterializeBindingResult(InvocationExpressionSyntax invocation, IMethodSymbol method, string result)
    {
        var owner = method.ContainingType.OriginalDefinition.ToDisplayString();
        var typeArgument = owner == "Workers.Ai" && method.Name == "RunAsync" && method.TypeArguments.Length == 2
            ? method.TypeArguments[1]
            : method.TypeArguments.FirstOrDefault()
                ?? (method.ContainingType.TypeArguments.Length == 1 ? method.ContainingType.TypeArguments[0] : null);
        if (typeArgument is null) return result;
        string? Converter(int mode) => JsonBoundaryConverter(typeArgument, invocation, mode);
        string? CloneConverter() => RpcArgumentReader(typeArgument, invocation) is { } reader
            ? $"value => {reader}(value, new WeakMap())" : null;
        string Then(string? converter) => converter is null ? result : $"{result}.then({converter})";
        string Call(string? converter) => converter is null ? result : $"({converter})({result})";
        string ThenMap(string? converter, string shape) => converter is null ? result : $"{result}.then(value => {string.Format(shape, converter)})";
        return (owner, method.Name) switch
        {
            ("Workers.ExecutionContext" or "Workers.DurableObjectState", "Props") => Call(CloneConverter()),
            ("Workers.Ai", "RunAsync") => Then(Converter(JsonWebMode)),
            ("Workers.Request" or "Workers.Response" or "Workers.R2ObjectBody", "JsonAsync") => Then(Converter(JsonWebMode)),
            ("Workers.KVNamespace", "GetJsonAsync")
                when method.Parameters[0].Type.SpecialType == SpecialType.System_String => Then(Converter(JsonWebMode)),
            ("Workers.KVNamespace", "GetJsonAsync") =>
                ThenMap(Converter(JsonWebMode), "Object.fromEntries(Object.entries(value).map(([key, item]) => [key, ({0})(item)]))"),
            ("Workers.DurableObjectStorage" or "Workers.DurableObjectTransaction", "GetAsync")
                when method.Parameters[0].Type.SpecialType == SpecialType.System_String => Then(CloneConverter()),
            ("Workers.DurableObjectStorage" or "Workers.DurableObjectTransaction", "GetAsync" or "ListAsync") =>
                ThenMap(CloneConverter(), "Object.fromEntries(Object.entries(value).map(([key, item]) => [key, ({0})(item)]))"),
            ("Workers.KVNamespace", "GetJsonWithMetadataAsync") =>
                ThenMap(Converter(JsonWebMode), "(value.value = ({0})(value.value), value)"),
            ("Workers.SyncKvStorage", "List") => CloneConverter() is { } entries
                ? $"(value => Object.fromEntries(Object.entries(value).map(([key, item]) => [key, ({entries})(item)])))({result})"
                : result,
            ("Workers.SyncKvStorage", "Get") or ("Workers.WebSocket", "DeserializeAttachment") => Call(CloneConverter()),
            ("Workers.Fetcher" or "Workers.RpcStub" or "Workers.RawBinding", "InvokeAsync") => Then(CloneConverter()),
            ("Workers.UrlSearchParams", "As") => typeArgument is INamedTypeSymbol queryType && IsUserInstanceType(queryType)
                && Converter(JsonQueryMode) is { } query
                    ? $"({query})({result})"
                    : throw new NotSupportedException($"WRK119: UrlSearchParams.As<T>() needs a Worker class or record type, not '{typeArgument}'."),
            ("Workers.D1PreparedStatement", "FirstAsync") => Then(Converter(JsonRowMode)),
            ("Workers.D1PreparedStatement", "AllAsync" or "RunAsync") =>
                ThenMap(Converter(JsonRowMode), "(value.results = value.results.map({0}), value)"),
            ("Workers.D1Database" or "Workers.D1DatabaseSession", "BatchAsync") =>
                ThenMap(Converter(JsonRowMode), "value.map(result => (result.results = result.results.map({0}), result))"),
            ("Workers.SqlStorageCursor<T>", "One" or "Next") => Call(Converter(JsonRowMode)),
            ("Workers.SqlStorageCursor<T>", "ToArray") =>
                Converter(JsonRowMode) is { } rows ? $"{result}.map({rows})" : result,
            _ => result
        };
    }

    // JSON read as a node is parsed from the text, so numbers a double cannot hold keep their digits.
    private string EmitBindingIntrinsicCore(
        string receiver,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        BindingIntrinsic intrinsic)
    {
        var arguments = BindingArguments(invocation, method);
        var instance = (_model.GetOperation(invocation) as IInvocationOperation)?.Instance;
        var checkReceiver = !method.IsStatic && NeedsNullCheck(instance);
        var parameterOrdinals = invocation.ArgumentList.Arguments.Select(argument => BoundParameter(argument).Ordinal).ToArray();
        if (parameterOrdinals.Where((ordinal, index) => ordinal != index).Any()
            || checkReceiver && invocation.ArgumentList.Arguments.Any(argument => !IsTrivialArgumentValue(BoundArgument(argument).Value)))
        {
            var key = $"binding:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}";
            var receiverTemporary = _names.Get(key + ":receiver", "receiver");
            var argumentTemporaries = arguments.Select((_, index) =>
                _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
            // Capture an explicit params array as one value, then spread it in the
            // native call. Spreading into the capture lambda would drop its tail.
            var rebound = arguments.Select((argument, index) => (argument.Parameter,
                Value: (argument.Value.StartsWith("...", StringComparison.Ordinal) ? "..." : "") + argumentTemporaries[index])).ToArray();
            var lastOrdinal = rebound.Max(argument => argument.Parameter.Ordinal);
            var ordered = new List<(IParameterSymbol Parameter, string Value)>();
            foreach (var parameter in method.Parameters.Take(lastOrdinal + 1))
            {
                var supplied = rebound.Where(argument => SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter)).ToArray();
                if (supplied.Length == 0) ordered.Add((parameter, "undefined"));
                else ordered.AddRange(supplied);
            }
            var body = EmitBindingIntrinsic(checkReceiver ? NotNull(receiverTemporary) : receiverTemporary, method, intrinsic, ordered);
            return $"(({string.Join(", ", new[] { receiverTemporary }.Concat(argumentTemporaries))}) => {body})"
                + $"({string.Join(", ", new[] { receiver }.Concat(arguments.Select(argument => argument.Value.StartsWith("...", StringComparison.Ordinal) ? argument.Value[3..] : argument.Value)))})";
        }
        return EmitBindingIntrinsic(checkReceiver ? NotNull(receiver) : receiver, method, intrinsic, arguments);
    }

    private string EmitBindingIntrinsic(
        string receiver,
        IMethodSymbol method,
        BindingIntrinsic intrinsic,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        if (intrinsic.Kind == BindingIntrinsicKind.ExportBinding
            || (method.ContainingType.ToDisplayString(), method.Name) is
            ("Workers.DurableObjectStorage" or "Workers.DurableObjectTransaction", "PutAsync")
            or ("Workers.SyncKvStorage", "Put") or ("Workers.WebSocket", "SerializeAttachment"))
            arguments = arguments.Select(argument =>
                (argument.Parameter, Value: argument.Parameter.Name is "value" or "entries" or "attachment" or "props"
                    && RpcResultNeedsConversion(argument.Parameter.Type)
                        ? $"{RequireHelperName(JavaScriptHelper.RpcArguments, "rpcValue")}({argument.Value})"
                        : argument.Value)).ToArray();
        if (intrinsic.Kind == BindingIntrinsicKind.ArrayArguments)
            arguments = arguments.Select(item => (item.Parameter,
                Value: ArrayBindingArgument(item.Parameter, item.Value))).ToArray();
        // These optional Web API arguments use null in C# to request the native default.
        var values = string.Join(", ", arguments.Select(item => intrinsic.NullDefaultsAreUndefined
            && item.Parameter.HasExplicitDefaultValue && item.Parameter.ExplicitDefaultValue is null
                ? $"({item.Value}) ?? undefined" : item.Value));
        if (method.ContainingType.ToDisplayString() == "Workers.Queue" && method.Name is "SendAsync" or "SendBatchAsync")
            return $"{RequireHelperName(JavaScriptHelper.QueueMessages, method.Name == "SendAsync" ? "queueSend" : "queueSendBatch")}({receiver}, {values})";
        return intrinsic.Kind switch
        {
            BindingIntrinsicKind.Direct or BindingIntrinsicKind.ArrayArguments => $"{receiver}.{intrinsic.JavascriptName}({values})",
            BindingIntrinsicKind.Property => $"{receiver}.{intrinsic.JavascriptName}",
            BindingIntrinsicKind.PropertyByName => $"{receiver}[{arguments[0].Value}]",
            BindingIntrinsicKind.Fluent => EmitFluentBinding(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.Dispose => $"{receiver}[Symbol.dispose]()",
            BindingIntrinsicKind.RequestJson => $"{RequireHelperName(arguments.Count == 0 ? JavaScriptHelper.HttpError : JavaScriptHelper.BoundedBody, "requestJson")}({string.Join(", ", new[] { receiver }.Concat(arguments.Select(item => item.Value)))})",
            BindingIntrinsicKind.RequestBodyRead => arguments.Count == 0
                ? $"{receiver}.{intrinsic.JavascriptName}()"
                : $"{RequireHelperName(JavaScriptHelper.BoundedBody, intrinsic.JavascriptName == "text" ? "requestText" : "requestBytes")}({receiver}, {arguments[0].Value})",
            BindingIntrinsicKind.KvGet => EmitKvGet(receiver, "get", arguments, intrinsic.JavascriptName),
            BindingIntrinsicKind.KvGetWithMetadata => EmitKvGet(receiver, "getWithMetadata", arguments, intrinsic.JavascriptName),
            BindingIntrinsicKind.KvJsonPut => EmitKvJsonPut(receiver, intrinsic.JavascriptName, arguments),
            // A failed onlyIf condition yields the object's metadata without a body; the API reports it as null.
            BindingIntrinsicKind.R2Get => $"{receiver}.get({values}).then(value => value != null && \"body\" in value ? value : null)",
            BindingIntrinsicKind.DurableObjectGet => EmitDurableObjectGet(receiver, method, arguments),
            BindingIntrinsicKind.DictionaryObject => EmitDictionaryObject(receiver, method, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.SyncStorageGet => $"({receiver}.get({arguments[0].Value}) ?? null)",
            BindingIntrinsicKind.SqlCursorNext => $"(row => row.done ? null : row.value)({receiver}.next())",
            BindingIntrinsicKind.AsyncIteratorNext => $"{receiver}.next().then(item => item.done ? null : item.value)",
            // Calling a loopback export with props specializes it; without props the export is the binding itself.
            BindingIntrinsicKind.ExportBinding => arguments.Count == 1
                ? $"{receiver}[{arguments[0].Value}]"
                : $"{receiver}[{arguments[0].Value}]({{ props: {arguments[1].Value} }})",
            BindingIntrinsicKind.CacheMatch => $"{receiver}.{intrinsic.JavascriptName}({values}).then(value => value ?? null)",
            BindingIntrinsicKind.Rpc => EmitRpc(receiver, method, arguments),
            BindingIntrinsicKind.DateResult => $"{receiver}.{intrinsic.JavascriptName}({values}).then(value => value == null ? null : new Date(value))",
            BindingIntrinsicKind.IteratorArray => $"Array.from({receiver}.{intrinsic.JavascriptName}())",
            // Every name maps to all of its values; scalar members take the first one.
            BindingIntrinsicKind.QueryAs =>
                $"((parameters) => Object.fromEntries(Array.from(new Set(parameters.keys()), name => [name, parameters.getAll(name)])))({receiver})",
            BindingIntrinsicKind.ReadableFromEnumerable => $"ReadableStream.from({arguments[0].Value})",
            BindingIntrinsicKind.WebSocketMessageText => $"(message => typeof message === \"string\" ? message : new TextDecoder().decode(message))({receiver})",
            BindingIntrinsicKind.WebSocketMessageBytes => $"(message => typeof message === \"string\" ? new TextEncoder().encode(message) : new Uint8Array(message))({receiver})",
            BindingIntrinsicKind.EventListener => intrinsic.JavascriptName == "message"
                ? $"{receiver}.addEventListener(\"message\", ((handler) => event => handler(event.data))({arguments[0].Value}))"
                : $"{receiver}.addEventListener(\"{intrinsic.JavascriptName}\", {arguments[0].Value})",
            BindingIntrinsicKind.CryptoDigestBody => EmitCryptoDigest(receiver, method, arguments),
            BindingIntrinsicKind.CryptoSignHmac => EmitCryptoHmac(receiver, method, arguments, verify: false),
            BindingIntrinsicKind.CryptoVerifyHmac => EmitCryptoHmac(receiver, method, arguments, verify: true),
            BindingIntrinsicKind.CryptoBytesResult => $"{receiver}.{intrinsic.JavascriptName}({values}).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoImportKey => $"{receiver}.importKey({(method.Parameters[0].Type.ToDisplayString() == "Workers.JsonWebKey" ? "\"jwk\", " : "")}{values})",
            BindingIntrinsicKind.CryptoExportJwk => $"{receiver}.exportKey(\"jwk\", {arguments[0].Value})",
            _ => throw new InvalidOperationException($"Unknown binding intrinsic kind '{intrinsic.Kind}'.")
        };
    }

    private static string ArrayBindingArgument(IParameterSymbol parameter, string value)
    {
        if (parameter.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T) return value;
        return parameter.HasExplicitDefaultValue && parameter.ExplicitDefaultValue is null
            ? $"(values => values == null ? undefined : Array.from(values))({value})"
            : $"Array.from({value})";
    }

    private static string EmitFluentBinding(string receiver, string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var parameters = arguments.Select((_, index) => $"arg{index}").ToArray();
        var names = string.Join(", ", parameters);
        var values = string.Join(", ", arguments.Select(argument => argument.Value));
        // Evaluate user expressions outside the adapter's scope, preserving their bindings and await context.
        return $"((value{(parameters.Length == 0 ? "" : ", " + names)}) => {{ value.{method}({names}); return value; }})"
            + $"({receiver}{(parameters.Length == 0 ? "" : ", " + values)})";
    }

    private IReadOnlyList<(IParameterSymbol Parameter, string Value)> BindingArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        // A params array passed as an array, rather than as separate values, is spread into the native call.
        var spreadParams = _model.GetOperation(invocation) is IInvocationOperation operation
            && operation.Arguments.Any(argument => argument.Parameter?.IsParams == true && argument.ArgumentKind == ArgumentKind.Explicit);
        var values = new List<(IParameterSymbol Parameter, string Value)>();
        for (var index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var argument = invocation.ArgumentList.Arguments[index];
            var parameter = BoundParameter(argument);
            var value = ArgumentValue(argument);
            values.Add((parameter, spreadParams && parameter.IsParams ? "..." + value : value));
        }
        return values;
    }

}
