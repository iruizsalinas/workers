using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Invocation(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)).ToArray();
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var containingType = method?.ContainingType.ToDisplayString();
        var methodName = method?.Name;
        if (containingType is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" && methodName == "FromResult") return arguments[0];
        if (containingType == "System.Threading.Tasks.Task" && methodName == "Delay")
        {
            if (arguments.Length != 1) throw UnsupportedSymbol(method, invocation);
            return $"{_helpers.Require(JavaScriptHelper.Delay)}({arguments[0]})";
        }
        if (TryEmitStaticInvocation(invocation, method, containingType, methodName, arguments, out var result)) return result;
        if (invocation.Expression is MemberAccessExpressionSyntax member)
            return MemberInvocation(invocation, member, method, containingType, arguments);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"this.{LowerFirst(method.Name.Replace("Async", "", StringComparison.Ordinal))}({string.Join(", ", arguments)})";
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0) return EmitUserInvocation(method, invocation, arguments);
        if (method is not null) throw UnsupportedSymbol(method, invocation);
        return $"{Expression(invocation.Expression)}({string.Join(", ", arguments)})";
    }

    private bool TryEmitStaticInvocation(InvocationExpressionSyntax invocation, IMethodSymbol? method, string? type, string? name, string[] arguments, out string result)
    {
        result = (type, name) switch
        {
            ("System.TimeSpan", "FromMilliseconds") => arguments[0],
            ("System.Console", "WriteLine") when arguments.Length == 1 => $"console.log({arguments[0]})",
            ("System.Guid", "NewGuid") => "globalThis.crypto.randomUUID()",
            ("Workers.Performance", "Now") => "performance.now()",
            ("System.Uri", "UnescapeDataString") => $"decodeURIComponent({arguments[0]})",
            ("System.Convert", "FromHexString") => $"Uint8Array.from({arguments[0]}.match(/../g) ?? [], value => Number.parseInt(value, 16))",
            ("System.Convert", "ToHexString") => $"Array.from({arguments[0]}, byte => byte.toString(16).padStart(2, \"0\")).join(\"\")",
            ("System.Text.Encoding", "GetBytes") => $"new TextEncoder().encode({arguments[0]})",
            ("int", "Parse") => $"Number.parseInt({arguments[0]}, 10)",
            ("System.Math", "Min" or "Max") => $"Math.{name!.ToLowerInvariant()}({string.Join(", ", arguments)})",
            ("System.Text.RegularExpressions.Regex", "IsMatch") => $"new RegExp({arguments[1]}).test({arguments[0]})",
            ("Workers.Timers", "SetTimeout") => $"setTimeout({arguments[0]}, {arguments[1]})",
            ("Workers.Timers", "ClearTimeout") => $"clearTimeout({arguments[0]})",
            ("Workers.Body", "Text" or "FromBytes") => arguments[0],
            ("Workers.Body", "Json") => $"JSON.stringify({arguments[0]})",
            ("Workers.Response", _) => ResponseInvocation(invocation, method, name!, arguments),
            _ => ""
        };
        return result.Length != 0;
    }

    private string MemberInvocation(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member, IMethodSymbol? method, string? type, string[] arguments)
    {
        var receiver = Expression(member.Expression);
        var name = member.Name.Identifier.Text;
        if (type == "System.IO.TextWriter" && name == "WriteLine"
            && _model.GetSymbolInfo(member.Expression).Symbol is IPropertySymbol { ContainingType: { } consoleType, Name: "Error" }
            && consoleType.ToDisplayString() == "System.Console")
            return arguments.Length == 1 ? $"console.error({arguments[0]})" : throw UnsupportedSymbol(method, invocation);
        if (TryEmitFrameworkInvocation(invocation, method, receiver, name, arguments, out var framework)) return framework;
        if (type == "Workers.Env" && EnvironmentBindings.Contains(name)) return $"{receiver}[{arguments[0]}]";
        if (type == "Workers.CacheStorage" && name == "OpenAsync") return $"caches.open({arguments[0]})";
        if (type == "Workers.Http" && name == "FetchAsync") return $"fetch({string.Join(", ", arguments)})";
        if (type == "Workers.WebSocketPair" && name == "Create") return "new WebSocketPair()";
        if (type == "Workers.TcpSocket" && name == "Connect") return SocketConnect(method, arguments);
        if (type == "Workers.Crypto") receiver = "globalThis.crypto";
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic)) return EmitBindingIntrinsic(receiver, invocation, method, intrinsic);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"{receiver}.{LowerFirst(method.Name.Replace("Async", "", StringComparison.Ordinal))}({string.Join(", ", arguments)})";
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0) return EmitUserInvocation(method, invocation, arguments);
        throw UnsupportedSymbol(method, invocation);
    }

    private string ResponseInvocation(InvocationExpressionSyntax invocation, IMethodSymbol? method, string name, string[] arguments) => name switch
    {
        "Text" => Response(arguments, "text"),
        "Html" => $"new Response({arguments[0]}, {{ status: {(arguments.Length > 1 ? arguments[1] : "200")}, headers: {{ \"content-type\": \"text/html; charset=utf-8\" }} }})",
        "Json" => $"Response.json({arguments[0]}{ResponseInit(arguments, 1)})",
        "Empty" => $"new Response(null{ResponseInit(arguments, 0)})",
        "Redirect" => $"Response.redirect({arguments[0]}, {(arguments.Length > 1 ? arguments[1] : "302")})",
        "FromBody" => $"new Response({arguments[0]}.body ?? {arguments[0]})",
        "FromStream" when method?.Parameters.Length >= 2 && method.Parameters[1].Type.ToDisplayString() == "Workers.Headers" => $"new Response({arguments[0]}, {{ status: {(arguments.Length > 2 ? arguments[2] : "200")}, headers: {arguments[1]} }})",
        "FromStream" => $"new Response({arguments[0]}{ResponseInit(arguments, 1)})",
        "WebSocket" => $"new Response(null, {{ status: 101, webSocket: {arguments[0]} }})",
        "WithHeader" => HeaderMutation(invocation, arguments, "set"),
        "AppendHeader" => HeaderMutation(invocation, arguments, "append"),
        "WithoutHeader" => HeaderMutation(invocation, arguments, "delete"),
        _ => ""
    };

    private string HeaderMutation(InvocationExpressionSyntax invocation, string[] arguments, string operation)
    {
        var helper = _helpers.Require(JavaScriptHelper.WithHeader);
        var receiver = Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        return operation switch
        {
            "set" => $"{helper}({receiver}, {arguments[0]}, {arguments[1]})",
            "append" => $"{helper}({receiver}, {arguments[0]}, {arguments[1]}, \"append\")",
            _ => $"{helper}({receiver}, {arguments[0]}, undefined, \"delete\")"
        };
    }

    private string SocketConnect(IMethodSymbol? method, string[] arguments)
    {
        var connect = _imports.Require("cloudflare:sockets", "connect", "connectSocket");
        if (method?.Parameters.Length >= 2 && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
            return $"{connect}({{ hostname: {arguments[0]}, port: {arguments[1]} }}{(arguments.Length > 2 ? $", {arguments[2]}" : "")})";
        return $"{connect}({string.Join(", ", arguments)})";
    }

    private static readonly HashSet<string> EnvironmentBindings =
    [
        "Get", "Variable", "Secret", "Raw", "Kv", "R2", "Service", "Assets", "Mtls", "Dispatcher", "Queue", "D1",
        "DurableObject", "RateLimiter", "Analytics", "Email", "Version", "Ai", "Workflow", "Images", "Media",
        "Vectorize", "SecretStore", "Hyperdrive"
    ];
}
