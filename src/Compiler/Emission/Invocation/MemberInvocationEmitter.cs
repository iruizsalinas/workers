using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string MemberInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        IMethodSymbol? method,
        string? type,
        string[] arguments,
        string? receiverOverride = null)
    {
        var name = method?.Name ?? member.Name.Identifier.ValueText;
        if (type == "System.IO.TextWriter" && name == "WriteLine"
            && _model.GetSymbolInfo(member.Expression).Symbol is IPropertySymbol { ContainingType: { } consoleType, Name: "Error" }
            && consoleType.ToDisplayString() == "System.Console")
            return arguments.Length == 1 ? $"console.error({arguments[0]})" : throw UnsupportedSymbol(method, invocation);
        var receiver = receiverOverride ?? Expression(member.Expression);
        if (TryEmitFrameworkInvocation(invocation, method, receiver, name, arguments, out var framework)) return framework;
        if (type == "Workers.Env" && EnvironmentBindings.Contains(name)) return $"{receiver}[{arguments[0]}]";
        if (type == "Workers.CacheStorage" && name == "OpenAsync") return $"caches.open({arguments[0]})";
        if (type == "Workers.Http" && name == "FetchAsync")
            return HttpFetch(invocation, method!, arguments);
        if (type == "Workers.WebSocketPair" && name == "Create") return "new WebSocketPair()";
        if (type == "Workers.TcpSocket" && name == "Connect") return SocketConnect(method, arguments);
        if (type == "Workers.Crypto") receiver = "globalThis.crypto";
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic)) return EmitBindingIntrinsic(receiver, invocation, method, intrinsic);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"{receiver}.{GeneratedInstanceMethodName(method)}({string.Join(", ", arguments)})";
        if (method is { IsStatic: false } && IsUserInstanceType(method.ContainingType))
        {
            QueueUserType(method.ContainingType, invocation);
            return $"{receiver}.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";
        }
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0) return EmitUserInvocation(method, invocation, arguments);
        throw UnsupportedSymbol(method, invocation);
    }

    private string HttpFetch(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        var tokenIndex = method.Parameters.FirstOrDefault(IsCancellationToken)?.Ordinal ?? -1;
        if (tokenIndex < 0) return $"fetch({string.Join(", ", arguments)})";
        var token = arguments[tokenIndex];
        var native = arguments.Where((_, index) => index != tokenIndex).ToArray();
        return native.Length switch
        {
            1 => $"fetch({native[0]}, {{ signal: {token} ?? undefined }})",
            2 => $"fetch({native[0]}, {{ ...{native[1]}, ...({token} == null ? {{}} : {{ signal: {token} }}) }})",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private static bool IsCancellationToken(IParameterSymbol parameter) =>
        parameter.Type.ToDisplayString() == "System.Threading.CancellationToken";

    private string ResponseInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        string name,
        string[] arguments,
        string? receiverOverride) => name switch
    {
        "Text" => Response(arguments, "text"),
        "Html" => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2, "{ \"content-type\": \"text/html; charset=utf-8\" }")})",
        "Json" => $"Response.json({arguments[0]}{JsonResponseInit(arguments)})",
        "Empty" => $"new Response(null{ResponseInit(arguments, 0, 1)})",
        "Redirect" when arguments.Length > 2 => $"new Response(null{ResponseInit(arguments, 1, 2, $"{{ location: {arguments[0]} }}")})",
        "Redirect" => $"Response.redirect({arguments[0]}, {(arguments.Length > 1 ? arguments[1] : "302")})",
        "FromBody" => ResponseFromBody(invocation, arguments),
        "FromStream" when method?.Parameters.Length >= 2 && method.Parameters[1].Type.ToDisplayString() == "Workers.Headers" => $"new Response({arguments[0]}, {{ status: {(arguments.Length > 2 ? arguments[2] : "200")}, headers: {arguments[1]} }})",
        "FromStream" => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2)})",
        "WebSocket" => $"new Response(null, {{ status: 101, webSocket: {arguments[0]} }})",
        "WithHeader" => HeaderMutation(invocation, arguments, "set", receiverOverride),
        "AppendHeader" => HeaderMutation(invocation, arguments, "append", receiverOverride),
        "WithoutHeader" => HeaderMutation(invocation, arguments, "delete", receiverOverride),
        _ => ""
    };

    private string ResponseFromBody(InvocationExpressionSyntax invocation, string[] arguments)
    {
        var body = _names.Get($"response-body:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}", "body");
        return $"(({body}) => new Response({body}.body ?? {body}{ResponseInit(arguments, 1, 2)}))({arguments[0]})";
    }

    private string HeaderMutation(
        InvocationExpressionSyntax invocation,
        string[] arguments,
        string operation,
        string? receiverOverride)
    {
        var helper = _helpers.Require(JavaScriptHelper.WithHeader);
        var receiver = receiverOverride ?? Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
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
