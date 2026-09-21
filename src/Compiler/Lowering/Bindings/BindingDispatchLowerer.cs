using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string EmitBindingIntrinsic(
        string receiver,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        BindingIntrinsic intrinsic)
    {
        var arguments = BindingArguments(invocation, method);
        var parameterOrdinals = invocation.ArgumentList.Arguments.Select((argument, index) => argument.NameColon is { } name
            ? method.Parameters.Single(parameter => parameter.Name == name.Name.Identifier.ValueText).Ordinal
            : Math.Min(index, method.Parameters.Length - 1)).ToArray();
        if (parameterOrdinals.Where((ordinal, index) => ordinal != index).Any()
            || arguments.Any(argument => IsCancellationToken(argument.Parameter)))
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
        var cancellation = arguments.FirstOrDefault(argument => IsCancellationToken(argument.Parameter)).Value;
        arguments = arguments.Where(argument => !IsCancellationToken(argument.Parameter)).ToArray();
        var result = intrinsic.Kind switch
        {
            BindingIntrinsicKind.Direct => $"{receiver}.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))})",
            BindingIntrinsicKind.KvBytesGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "arrayBuffer"),
            BindingIntrinsicKind.KvJsonGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "json"),
            BindingIntrinsicKind.KvJsonPut => EmitKvJsonPut(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.DurableObjectGet => EmitDurableObjectGet(receiver, method, arguments),
            BindingIntrinsicKind.CacheQuery => EmitCacheQuery(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.CacheMatch => $"{EmitCacheQuery(receiver, intrinsic.JavascriptName, arguments)}.then(value => value ?? null)",
            BindingIntrinsicKind.ServiceRpc => EmitServiceRpc(receiver, arguments),
            BindingIntrinsicKind.Property => $"{receiver}.{intrinsic.JavascriptName}",
            BindingIntrinsicKind.Identity => receiver,
            BindingIntrinsicKind.HeadersClone => $"new Headers({receiver})",
            BindingIntrinsicKind.Fluent => $"(value => {{ value.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))}); return value; }})({receiver})",
            BindingIntrinsicKind.JsonParse => $"JSON.parse({receiver}.{intrinsic.JavascriptName})",
            BindingIntrinsicKind.Dispose => $"{receiver}[Symbol.asyncDispose]?.()",
            BindingIntrinsicKind.RateLimit => $"{receiver}.{intrinsic.JavascriptName}({{ key: {arguments.Single(item => item.Parameter.Name == "key").Value} }})",
            BindingIntrinsicKind.CryptoRandomBytes => $"{receiver}.getRandomValues(new Uint8Array({arguments[0].Value}))",
            BindingIntrinsicKind.CryptoTimingSafeEqual => $"{receiver}.subtle.timingSafeEqual({arguments[0].Value}, {arguments[1].Value})",
            BindingIntrinsicKind.CryptoDigestStream => $"new {receiver}.DigestStream({arguments[0].Value})",
            BindingIntrinsicKind.CryptoDigestText => $"{receiver}.subtle.digest({arguments[0].Value}, new TextEncoder().encode({arguments[1].Value})).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoDigest => $"{receiver}.subtle.digest({arguments[0].Value}, {arguments[1].Value}.body ?? {arguments[1].Value}).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoDigestBody => $"new Response({arguments[1].Value}.body ?? {arguments[1].Value}).arrayBuffer().then(value => {receiver}.subtle.digest({arguments[0].Value}, value)).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.DigestWrite => EmitDigestWrite(receiver, arguments[0].Value),
            BindingIntrinsicKind.DigestWriteText => EmitDigestWrite(receiver, $"new TextEncoder().encode({arguments[0].Value})"),
            BindingIntrinsicKind.DigestClose => EmitDigestClose(receiver),
            BindingIntrinsicKind.DigestResult => $"{receiver}.digest.then(value => new Uint8Array(value))",
            BindingIntrinsicKind.ReadableFromEnumerable => EmitReadableFrom(arguments[0].Value),
            BindingIntrinsicKind.ReadableRead => EmitReadableRead(receiver),
            BindingIntrinsicKind.ReadableAll => EmitReadableAll(receiver),
            BindingIntrinsicKind.WebSocketEvents => EmitWebSocketEvents(receiver),
            BindingIntrinsicKind.SocketRead => EmitSocketRead(receiver),
            BindingIntrinsicKind.SocketWrite => EmitSocketWrite(receiver, arguments[0].Value),
            BindingIntrinsicKind.SocketWriteText => EmitSocketWrite(receiver, $"new TextEncoder().encode({arguments[0].Value})"),
            BindingIntrinsicKind.SocketCloseWritable => EmitSocketCloseWritable(receiver),
            BindingIntrinsicKind.WebSocketJson => $"{receiver}.send(JSON.stringify({arguments[0].Value}))",
            BindingIntrinsicKind.WebSocketMessageText => $"typeof {receiver} === \"string\" ? {receiver} : new TextDecoder().decode({receiver})",
            BindingIntrinsicKind.Bytes => $"{receiver}.{intrinsic.JavascriptName}().then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoVerifyHmac => $"(async () => {{ const key = await {receiver}.subtle.importKey(\"raw\", new TextEncoder().encode({arguments[0].Value}), {{ name: \"HMAC\", hash: \"SHA-256\" }}, false, [\"verify\"]); return {receiver}.subtle.verify(\"HMAC\", key, {arguments[1].Value}, {arguments[2].Value}); }})()",
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
            _ => throw new InvalidOperationException($"Unknown binding intrinsic kind '{intrinsic.Kind}'.")
        };
        return cancellation is null
            ? result
            : $"({HelperInvocation(JavaScriptHelper.CancellationCheck, [cancellation])}, {result})";
    }

    private string EmitDigestWrite(string receiver, string value) { _helpers.Require(JavaScriptHelper.Digest); return $"{_helpers.Name("digestWriter")}({receiver}).write({value})"; }
    private string EmitDigestClose(string receiver) { _helpers.Require(JavaScriptHelper.Digest); return $"{_helpers.Name("digestWriter")}({receiver}).close()"; }
    private string EmitReadableFrom(string value) { _helpers.Require(JavaScriptHelper.Stream); return $"{_helpers.Name("streamFrom")}({value})"; }
    private string EmitReadableRead(string receiver) { _helpers.Require(JavaScriptHelper.Stream); return $"{_helpers.Name("streamRead")}({receiver})"; }
    private string EmitReadableAll(string receiver) { _helpers.Require(JavaScriptHelper.Stream); return $"{_helpers.Name("streamAll")}({receiver})"; }
    private string EmitWebSocketEvents(string receiver) { _helpers.Require(JavaScriptHelper.WebSocketEvents); return $"{_helpers.Name("webSocketEvents")}({receiver})"; }
    private string EmitSocketRead(string receiver) { _helpers.Require(JavaScriptHelper.Socket); return $"{_helpers.Name("socketRead")}({receiver})"; }
    private string EmitSocketWrite(string receiver, string value) { _helpers.Require(JavaScriptHelper.Socket); return $"{_helpers.Name("socketWriter")}({receiver}).write({value})"; }
    private string EmitSocketCloseWritable(string receiver) { _helpers.Require(JavaScriptHelper.Socket); return $"{_helpers.Name("socketWriter")}({receiver}).close()"; }

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
