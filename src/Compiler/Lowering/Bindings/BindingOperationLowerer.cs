using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    // KV reads choose the value type with the native "type" option. Binary values arrive as ArrayBuffers
    // and are exposed as byte arrays; reads of several keys return a Map, exposed as a dictionary.
    private static string EmitKvGet(
        string receiver,
        string method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments,
        string type)
    {
        var key = arguments.Single(item => item.Parameter.Name is "key" or "keys").Value;
        var multiple = arguments.Any(item => item.Parameter.Name == "keys");
        if (multiple) key = $"Array.from({key})";
        var options = arguments.FirstOrDefault(item => item.Parameter.Name == "options").Value;
        var invocation = $"{receiver}.{method}({key}{KvGetOptions(options, type)})";
        if (multiple)
            return $"{invocation}.then(value => Object.fromEntries(value))";
        if (type != "arrayBuffer")
            return invocation;
        return method == "get"
            ? $"{invocation}.then(value => value == null ? null : new Uint8Array(value))"
            : $"{invocation}.then(result => (result.value = result.value == null ? null : new Uint8Array(result.value), result))";
    }

    // Text is the runtime's default read type, so its options pass through; other types add the type option,
    // merged into a literal dictionary when the options were written inline.
    private static string KvGetOptions(string? options, string type)
    {
        var literal = options is not null && options.StartsWith('{') && options.EndsWith('}');
        if (type == "text")
            return options is null ? "" : literal ? $", {options}" : $", {options} ?? undefined";
        if (options is null || options == "{}")
            return $", {{ type: \"{type}\" }}";
        return literal
            ? $", {options[..^1].TrimEnd()}, type: \"{type}\" }}"
            : $", {{ ...{options}, type: \"{type}\" }}";
    }

    private static string EmitDurableObjectGet(
        string receiver,
        IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var invocation = $"{receiver}.get({string.Join(", ", arguments.Select(item => item.Parameter.Name == "keys" ? $"Array.from({item.Value})" : item.Value))})";
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

    // RPC arguments are a params array; compiled C# class and record instances cross as plain objects.
    private string EmitRpc(
        string receiver,
        IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var name = arguments.Single(item => item.Parameter.Name == "methodName").Value;
        var values = arguments.Where(item => item.Parameter.IsParams).Select(item => item.Value).ToArray();
        var rpcArguments = _helpers.Require(JavaScriptHelper.RpcArguments);
        return values.Length == 0
            ? $"{receiver}[{name}]()"
            : $"{receiver}[{name}](...{rpcArguments}([{string.Join(", ", values)}]))";
    }
}
