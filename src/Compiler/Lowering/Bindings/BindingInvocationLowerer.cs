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
        return intrinsic.Kind switch
        {
            BindingIntrinsicKind.Direct => $"{receiver}.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))})",
            BindingIntrinsicKind.KvBytesGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "arrayBuffer"),
            BindingIntrinsicKind.KvJsonGet => EmitKvGet(receiver, intrinsic.JavascriptName, arguments, "json"),
            BindingIntrinsicKind.KvJsonPut => EmitKvJsonPut(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.CacheQuery => EmitCacheQuery(receiver, intrinsic.JavascriptName, arguments),
            BindingIntrinsicKind.CacheMatch => $"{EmitCacheQuery(receiver, intrinsic.JavascriptName, arguments)}.then(value => value ?? null)",
            BindingIntrinsicKind.ServiceRpc => EmitServiceRpc(receiver, arguments),
            BindingIntrinsicKind.Property => $"{receiver}.{intrinsic.JavascriptName}",
            BindingIntrinsicKind.Identity => receiver,
            BindingIntrinsicKind.Fluent => $"(value => {{ value.{intrinsic.JavascriptName}({string.Join(", ", arguments.Select(item => item.Value))}); return value; }})({receiver})",
            BindingIntrinsicKind.JsonParse => $"JSON.parse({receiver}.{intrinsic.JavascriptName})",
            BindingIntrinsicKind.Dispose => $"{receiver}[Symbol.asyncDispose]?.()",
            BindingIntrinsicKind.RateLimit => $"{receiver}.{intrinsic.JavascriptName}({{ key: {arguments.Single(item => item.Parameter.Name == "key").Value} }})",
            BindingIntrinsicKind.CryptoRandomBytes => $"{receiver}.getRandomValues(new Uint8Array({arguments[0].Value}))",
            BindingIntrinsicKind.CryptoTimingSafeEqual => $"{receiver}.subtle.timingSafeEqual({arguments[0].Value}, {arguments[1].Value})",
            BindingIntrinsicKind.CryptoDigestStream => $"new {receiver}.DigestStream({arguments[0].Value})",
            BindingIntrinsicKind.CryptoDigestText => $"{receiver}.subtle.digest({arguments[0].Value}, new TextEncoder().encode({arguments[1].Value})).then(value => new Uint8Array(value))",
            BindingIntrinsicKind.CryptoDigest => $"{receiver}.subtle.digest({arguments[0].Value}, {arguments[1].Value}.body ?? {arguments[1].Value}).then(value => new Uint8Array(value))",
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
            BindingIntrinsicKind.BodyText => $"typeof {receiver} === \"string\" ? {receiver} : new TextDecoder().decode({receiver})",
            BindingIntrinsicKind.BodyJson => $"JSON.parse(typeof {receiver} === \"string\" ? {receiver} : new TextDecoder().decode({receiver}))",
            BindingIntrinsicKind.Bytes => $"{receiver}.{intrinsic.JavascriptName}().then(value => new Uint8Array(value))",
            _ => throw new InvalidOperationException($"Unknown binding intrinsic kind '{intrinsic.Kind}'.")
        };
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
                ? method.Parameters.Single(item => item.Name == name.Name.Identifier.Text)
                : method.Parameters[Math.Min(index, method.Parameters.Length - 1)];
            if (parameter.Type.ToDisplayString() == "System.Threading.CancellationToken")
                continue;
            values.Add((parameter, Expression(argument.Expression)));
        }
        return values;
    }

    private static string EmitKvGet(
        string receiver,
        string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments,
        string type)
    {
        var key = arguments.Single(item => item.Parameter.Name is "key" or "keys").Value;
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        var nativeOptions = options is null ? $"{{ type: \"{type}\" }}" : $"{{ ...{options}, type: \"{type}\" }}";
        return $"{receiver}.{method}({key}, {nativeOptions})";
    }

    private static string EmitKvJsonPut(
        string receiver,
        string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var key = arguments.Single(item => item.Parameter.Name == "key").Value;
        var value = arguments.Single(item => item.Parameter.Name == "value").Value;
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        return $"{receiver}.{method}({key}, JSON.stringify({value}){(options is null ? "" : ", " + options)})";
    }

    private static string EmitCacheQuery(
        string receiver,
        string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var key = arguments[0].Value;
        var option = arguments.Skip(1).FirstOrDefault().Value;
        if (option is null)
            return $"{receiver}.{method}({key})";
        var parameter = arguments[1].Parameter;
        var nativeOption = parameter.Name == "ignoreMethod" ? $"{{ ignoreMethod: {option} }}" : option;
        return $"{receiver}.{method}({key}, {nativeOption})";
    }

    private static string EmitServiceRpc(
        string receiver,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var method = arguments.Single(item => item.Parameter.Name == "methodName").Value;
        var values = arguments.FirstOrDefault(item => item.Parameter.Name == "arguments").Value;
        return values is null ? $"{receiver}[{method}]()" : $"{receiver}[{method}](...({values} ?? []))";
    }
}

