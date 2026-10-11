using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var typeArgument = method.TypeArguments.FirstOrDefault()
            ?? (method.ContainingType.TypeArguments.Length == 1 ? method.ContainingType.TypeArguments[0] : null);
        if (typeArgument is null) return result;
        string? Converter(int mode) => JsonBoundaryConverter(typeArgument, invocation, mode);
        string Then(string? converter) => converter is null ? result : $"{result}.then({converter})";
        string Call(string? converter) => converter is null ? result : $"({converter})({result})";
        string ThenMap(string? converter, string shape) => converter is null ? result : $"{result}.then(value => {string.Format(shape, converter)})";
        return (owner, method.Name) switch
        {
            ("Workers.Request" or "Workers.Response", "JsonAsync") => Then(Converter(JsonWebMode)),
            ("Workers.IKvNamespace", "GetJsonAsync") => Then(Converter(JsonWebMode)),
            ("Workers.IKvNamespace", "GetJsonWithMetadataAsync") =>
                ThenMap(Converter(JsonWebMode), "(value.value = ({0})(value.value), value)"),
            ("Workers.IKvNamespace", "GetJsonBulkAsync") =>
                ThenMap(Converter(JsonWebMode), "Object.fromEntries(Object.entries(value).map(([key, item]) => [key, ({0})(item)]))"),
            ("Workers.QueryParameters", "As") => typeArgument is INamedTypeSymbol queryType && IsUserInstanceType(queryType)
                && Converter(JsonQueryMode) is { } query
                    ? $"({query})({result})"
                    : throw new NotSupportedException($"WRK119: QueryParameters.As<T>() needs a Worker class or record type, not '{typeArgument}'."),
            ("Workers.WebSocket", "DeserializeAttachment") => Call(Converter(JsonWebMode)),
            ("Workers.D1PreparedStatement", "FirstAsync") => Then(Converter(JsonRowMode)),
            ("Workers.D1PreparedStatement", "AllAsync") =>
                ThenMap(Converter(JsonRowMode), "(value.results = value.results.map({0}), value)"),
            ("Workers.ID1Database" or "Workers.D1DatabaseSession", "BatchAsync") =>
                ThenMap(Converter(JsonRowMode), "value.map(result => (result.results = result.results.map({0}), result))"),
            ("Workers.DurableObjectStorage" or "Workers.DurableObjectTransaction", "GetAsync")
                when method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                Then(Converter(JsonWebMode)),
            ("Workers.DurableObjectKvStorage", "Get") => Call(Converter(JsonWebMode)),
            ("Workers.DurableObjectSqlCursor<T>", "One" or "Next") => Call(Converter(JsonRowMode)),
            ("Workers.DurableObjectSqlCursor<T>", "ToArray") =>
                Converter(JsonRowMode) is { } rows ? $"{result}.map({rows})" : result,
            ("Workers.R2ObjectBody", "JsonAsync") => Then(Converter(JsonWebMode)),
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
        var parameterOrdinals = invocation.ArgumentList.Arguments.Select((argument, index) => argument.NameColon is { } name
            ? method.Parameters.Single(parameter => parameter.Name == name.Name.Identifier.ValueText).Ordinal
            : Math.Min(index, method.Parameters.Length - 1)).ToArray();
        if (parameterOrdinals.Where((ordinal, index) => ordinal != index).Any())
        {
            var key = $"binding:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}";
            var receiverTemporary = _names.Get(key + ":receiver", "receiver");
            var argumentTemporaries = arguments.Select((_, index) =>
                _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
            var rebound = arguments.Select((argument, index) => (argument.Parameter, Value: argumentTemporaries[index])).ToArray();
            var lastOrdinal = rebound.Max(argument => argument.Parameter.Ordinal);
            var ordered = new List<(IParameterSymbol Parameter, string Value)>();
            foreach (var parameter in method.Parameters.Take(lastOrdinal + 1))
            {
                var supplied = rebound.Where(argument => SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter)).ToArray();
                if (supplied.Length == 0) ordered.Add((parameter, "undefined"));
                else ordered.AddRange(supplied);
            }
            var body = EmitBindingIntrinsic(receiverTemporary, method, intrinsic, ordered);
            return $"(({string.Join(", ", new[] { receiverTemporary }.Concat(argumentTemporaries))}) => {body})"
                + $"({string.Join(", ", new[] { receiver }.Concat(arguments.Select(argument => argument.Value)))})";
        }
        return EmitBindingIntrinsic(receiver, method, intrinsic, arguments);
    }

    private string EmitBindingIntrinsic(
        string receiver,
        IMethodSymbol method,
        BindingIntrinsic intrinsic,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        return intrinsic.Kind switch
        {
            BindingIntrinsicKind.Direct => $"{receiver}.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))})",
            BindingIntrinsicKind.KvBytesGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "arrayBuffer"),
            BindingIntrinsicKind.KvJsonGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "json"),
            BindingIntrinsicKind.KvJsonPut => EmitKvJsonPut(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.DurableObjectGet => EmitDurableObjectGet(receiver, method, arguments),
            BindingIntrinsicKind.CacheMatch => $"{receiver}.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))}).then(value => value ?? null)",
            BindingIntrinsicKind.ServiceRpc => EmitServiceRpc(receiver, arguments),
            BindingIntrinsicKind.Property => $"{receiver}.{intrinsic.JavascriptName}",
            BindingIntrinsicKind.HeadersClone => $"new Headers({receiver})",
            BindingIntrinsicKind.Fluent => $"(value => {{ value.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))}); return value; }})({receiver})",
            BindingIntrinsicKind.Dispose => $"{receiver}[Symbol.dispose]()",
            BindingIntrinsicKind.RateLimit => $"{receiver}.{intrinsic.JavascriptName}({{ key: {arguments.Single(item => item.Parameter.Name == "key").Value} }})",
            BindingIntrinsicKind.CryptoRandomBytes => $"{receiver}.getRandomValues(new Uint8Array({arguments[0].Value}))",
            BindingIntrinsicKind.CryptoTimingSafeEqual => $"{receiver}.subtle.timingSafeEqual({arguments[0].Value}, {arguments[1].Value})",
            BindingIntrinsicKind.CryptoDigestStream => $"new {receiver}.DigestStream({arguments[0].Value})",
            BindingIntrinsicKind.CryptoDigestText => $"{receiver}.subtle.digest({arguments[0].Value}, new TextEncoder().encode({arguments[1].Value})).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoDigest => $"{receiver}.subtle.digest({arguments[0].Value}, {arguments[1].Value}).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoDigestBody => $"((crypto, algorithm, body) => new Response(body).arrayBuffer().then(value => crypto.subtle.digest(algorithm, value)).then(value => new Uint8Array(value)))({receiver}, {arguments[0].Value}, {arguments[1].Value})",
            BindingIntrinsicKind.DigestResult => $"{receiver}.digest.then(value => new Uint8Array(value))",
            BindingIntrinsicKind.ReadableFromEnumerable => EmitReadableFrom(arguments[0].Value),
            BindingIntrinsicKind.WebSocketJson => $"{receiver}.send(JSON.stringify({arguments[0].Value}))",
            BindingIntrinsicKind.WebSocketMessageText => $"(typeof {receiver} === \"string\" ? {receiver} : new TextDecoder().decode({receiver}))",
            BindingIntrinsicKind.CryptoVerifyHmac => $"(async (crypto, secret, signature, payload) => {{ const key = await crypto.subtle.importKey(\"raw\", new TextEncoder().encode(secret), {{ name: \"HMAC\", hash: \"SHA-256\" }}, false, [\"verify\"]); return crypto.subtle.verify(\"HMAC\", key, signature, payload); }})({receiver}, {arguments[0].Value}, {arguments[1].Value}, {arguments[2].Value})",
            BindingIntrinsicKind.BlobSliceBytes => $"{receiver}.slice({arguments[0].Value}, {arguments[1].Value}).arrayBuffer().then(value => new Uint8Array(value))",
            BindingIntrinsicKind.QueryNames => $"Array.from({receiver}.{intrinsic.JavascriptName}())",
            BindingIntrinsicKind.CompressStream => $"{receiver}.pipeThrough(new CompressionStream({arguments[0].Value}))",
            BindingIntrinsicKind.DecompressStream => $"{receiver}.pipeThrough(new DecompressionStream({arguments[0].Value}))",
            BindingIntrinsicKind.DictionaryObject => EmitDictionaryObject(receiver, method, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.QueueSend => EmitQueueSend(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.QueueSendBatch => EmitQueueSendBatch(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.QueueRequest => EmitQueueRequest(intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.RequestWithUrl => $"new Request({arguments[0].Value}, {receiver})",
            BindingIntrinsicKind.Utf8Decode => EmitUtf8Decode(arguments),
            BindingIntrinsicKind.SqlCursorNext => $"(row => row.done ? null : row.value)({receiver}.next())",
            BindingIntrinsicKind.DateResult => $"{receiver}.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))}).then(value => value == null ? null : new Date(value))",
            BindingIntrinsicKind.IteratorArray => $"Array.from({receiver}.{intrinsic.JavascriptName}())",
            BindingIntrinsicKind.ResponseRead => $"new Response({receiver}).{intrinsic.JavascriptName}()",
            BindingIntrinsicKind.WebSocketMessageBytes => $"(typeof {receiver} === \"string\" ? new TextEncoder().encode({receiver}) : new Uint8Array({receiver}))",
            BindingIntrinsicKind.EventListener => intrinsic.JavascriptName == "message"
                ? $"{receiver}.addEventListener(\"message\", event => ({arguments[0].Value})(event.data))"
                : $"{receiver}.addEventListener(\"{intrinsic.JavascriptName}\", {arguments[0].Value})",
            BindingIntrinsicKind.SyncStorageGet => $"({receiver}.get({arguments[0].Value}) ?? null)",
            // Every name maps to all of its values; scalar members take the first one.
            BindingIntrinsicKind.QueryAs =>
                $"((parameters) => Object.fromEntries(Array.from(new Set(parameters.keys()), name => [name, parameters.getAll(name)])))({receiver})",
            _ => throw new InvalidOperationException($"Unknown binding intrinsic kind '{intrinsic.Kind}'.")
        };
    }

    private string EmitReadableFrom(string value) => $"{_helpers.Require(JavaScriptHelper.StreamFrom)}({value})";

    private IReadOnlyList<(IParameterSymbol Parameter, string Value)> BindingArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        var values = new List<(IParameterSymbol Parameter, string Value)>();
        for (var index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var argument = invocation.ArgumentList.Arguments[index];
            var parameter = argument.NameColon is { } name
                ? method.Parameters.Single(item => item.Name == name.Name.Identifier.ValueText)
                : method.Parameters[Math.Min(index, method.Parameters.Length - 1)];
            values.Add((parameter, Expression(argument.Expression)));
        }
        return values;
    }

}
