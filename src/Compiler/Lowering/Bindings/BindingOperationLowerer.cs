using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static string EmitKvGet(
        string receiver,
        string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments,
        string type)
    {
        var key = arguments.Single(item => item.Parameter.Name is "key" or "keys").Value;
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        var nativeOptions = options is null ? $"{{ type: \"{type}\" }}" : $"{{ ...{options}, type: \"{type}\" }}";
        var invocation = $"{receiver}.{method}({key}, {nativeOptions})";
        return arguments.Any(item => item.Parameter.Name == "keys")
            ? $"{invocation}.then(value => Object.fromEntries(value))"
            : invocation;
    }

    private static string EmitDurableObjectGet(
        string receiver,
        IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var invocation = $"{receiver}.get({string.Join(", ", arguments.Select(item => item.Value))})";
        return method.Parameters[0].Type.SpecialType == SpecialType.System_String
            ? $"{invocation}.then(value => value ?? null)"
            : $"{invocation}.then(value => Object.fromEntries(value))";
    }

    private static string EmitDictionaryObject(
        string receiver,
        IMethodSymbol method,
        string name,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var invocation = $"{receiver}.{name}({string.Join(", ", arguments.Select(item => item.Value))})";
        return method.ReturnType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>"
            ? $"{invocation}.then(value => Object.fromEntries(value))"
            : $"Object.fromEntries({invocation})";
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

    private string EmitServiceRpc(
        string receiver,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var method = arguments.Single(item => item.Parameter.Name == "methodName").Value;
        var values = arguments.FirstOrDefault(item => item.Parameter.Name == "arguments").Value;
        return values is null
            ? $"{receiver}[{method}]()"
            : $"{receiver}[{method}](...{_helpers.Require(JavaScriptHelper.RpcArguments)}({values}))";
    }

    private static string EmitQueueSend(
        string receiver,
        string contentType,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var message = arguments.Single(item => item.Parameter.Name == "message").Value;
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        var nativeOptions = options is null
            ? $"{{ contentType: \"{contentType}\" }}"
            : $"{{ ...({options} ?? {{}}), contentType: \"{contentType}\" }}";
        return $"{receiver}.send({message}, {nativeOptions})";
    }

    private static string EmitQueueSendBatch(
        string receiver,
        string contentType,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var messages = arguments.Single(item => item.Parameter.Name == "messages").Value;
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        var requests = $"Array.from({messages}, body => ({{ body, contentType: \"{contentType}\" }}))";
        return $"{receiver}.sendBatch({requests}{(options is null ? "" : ", " + options)})";
    }

    private static string EmitQueueRequest(
        string contentType,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var body = arguments.Single(item => item.Parameter.Name == "body").Value;
        var delay = arguments.FirstOrDefault(item => item.Parameter.Name == "delaySeconds").Value;
        return $"{{ body: {body}, contentType: \"{contentType}\"{(delay is null ? "" : $", delaySeconds: {delay} ?? undefined")} }}";
    }

    private static string EmitUtf8Decode(
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var bytes = arguments.Single(item => item.Parameter.Name == "bytes").Value;
        var fatal = arguments.FirstOrDefault(item => item.Parameter.Name == "fatal").Value ?? "false";
        var ignoreBom = arguments.FirstOrDefault(item => item.Parameter.Name == "ignoreBom").Value ?? "false";
        return $"new TextDecoder(\"utf-8\", {{ fatal: {fatal}, ignoreBOM: {ignoreBom} }}).decode({bytes})";
    }
}
