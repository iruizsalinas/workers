using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string MemberInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax? member,
        IMethodSymbol? method,
        string? type,
        string[] arguments,
        string? receiverOverride = null)
    {
        var name = method?.Name ?? throw Unsupported("WRK105", invocation);
        var receiver = receiverOverride ?? (method?.IsStatic == true ? "" : member is null ? "this" : Expression(member.Expression));
        if (IsSynthesizedRecordToString(method))
            return RecordText(method!.ContainingType, receiver, invocation);
        if (IsSynthesizedRecordMember(method))
            return method is { Name: "Equals", Parameters: [{ } other] }
                   && SymbolEqualityComparer.Default.Equals(other.Type.OriginalDefinition, method.ContainingType.OriginalDefinition)
                ? RecordEquals(method.ContainingType, receiver, arguments[0], invocation)
                : throw UnsupportedSymbol(method, invocation);
        if (TryEmitFoundationInvocation(invocation, method, receiver, name, arguments, out var framework)) return framework;
        if (type == "Workers.Env" && EnvironmentBindings.Contains(name)) return $"{receiver}[{arguments[0]}]";
        if (type == "Workers.CacheStorage" && name == "OpenAsync") return $"caches.open({arguments[0]})";
        if (type == "Workers.Http" && name == "FetchAsync") return $"fetch({string.Join(", ", arguments)})";
        if (type == "Workers.WebSocketPair" && name == "Create") return "new WebSocketPair()";
        if (type == "Workers.TcpSocket" && name == "Connect") return SocketConnect(method, arguments);
        if (type == "Workers.Router" && method is not null) return RouterInvocation(invocation, method, receiver, arguments);
        if (type == "Workers.RouteContext" && name == "Parameter") return RouteParameterInvocation(receiver, arguments);
        if (type == "Workers.Cors" && name == "Allow") return HelperInvocation(JavaScriptHelper.Cors, arguments);
        if (type == "Workers.Cookies" && name is "Get" or "GetAll" or "Contains")
            return $"{receiver}.{(name == "Contains" ? "has" : LowerFirst(name))}({arguments[0]})";
        if (type == "Workers.ResponseCookies" && name is "Set" or "Delete")
            return ResponseCookieInvocation(receiver, name, arguments);
        if (type == "Workers.Crypto") receiver = "globalThis.crypto";
        if (type == "Workers.SubtleCrypto" && method?.IsStatic == true) receiver = "globalThis.SubtleCrypto";
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

    private string ResponseCookieInvocation(string receiver, string name, string[] arguments)
    {
        var cookie = name == "Set"
            ? new[] { arguments[0], arguments[1], arguments.Length > 2 ? arguments[2] : "undefined" }
            : new[] { arguments[0], "\"\"", arguments.Length > 1 ? arguments[1] : "undefined", "true" };
        return $"{receiver}.append(\"set-cookie\", {HelperInvocation(JavaScriptHelper.SetCookie, cookie)})";
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
