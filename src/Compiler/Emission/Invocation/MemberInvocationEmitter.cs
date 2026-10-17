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
        if (method is { Name: "ToString", Parameters.Length: 0, ContainingType: { } owner } && owner.ToDisplayString() == "System.Type")
            return $"{receiver}.text";
        if (TryEmitRecordInvocation(invocation, method, receiver, arguments, member?.Expression is BaseExpressionSyntax, out var record))
            return record;
        if (method is { Name: "ToString", Parameters.Length: 0, ContainingType.IsTupleType: true })
            return $"((tuple) => {TupleText(method.ContainingType, "tuple", invocation)})({receiver})";
        if (method is { Name: "ToString", Parameters.Length: 0 } && IsKeyValuePair(method.ContainingType))
            return KeyValuePairText(method.ContainingType, receiver, invocation);
        if (method is { Name: "ToString", Parameters.Length: 0, ContainingType.SpecialType: SpecialType.System_Object })
            return $"((value) => {{ if (value == null) throw {NullReferenceException()}; return {ObjectText("value")}; }})({receiver})";
        if (method is not null && member?.Expression is BaseExpressionSyntax && IsUserInstanceType(method.ContainingType))
            return $"{QueueUserType(method.ContainingType, invocation)}.prototype.{UserInstanceMethodName(method)}.call(this{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})";
        if (method is not null && IsSourceInterface(method.ContainingType))
            return method.IsStatic ? EmitUserInvocation(method, invocation, arguments)
                : method.DeclaredAccessibility == Accessibility.Private
                    ? $"{QueueUserMethod(method, invocation)}.call({receiver}{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})"
                : SourceInterfaceInvocation(method, receiver, arguments, invocation);
        if (TryEmitFoundationInvocation(invocation, method, receiver, name, arguments, out var framework)) return framework;
        if (type == "Workers.Env" && EnvironmentBindings.Contains(name)) return $"{receiver}[{arguments[0]}]";
        // A string variable is read as configured; typed variables are JSON values mapped to C# types.
        if (type == "Workers.Env" && name == "Variable")
            return method!.TypeArguments.FirstOrDefault() is { } variableType
                   && JsonBoundaryConverter(variableType, invocation, JsonWebMode) is { } convertVariable
                ? $"({convertVariable})({receiver}[{arguments[0]}])"
                : $"{receiver}[{arguments[0]}]";
        if (type == "Workers.CacheStorage" && name == "OpenAsync") return $"caches.open({arguments[0]})";
        if (type == "Workers.Http" && name == "FetchAsync") return $"fetch({string.Join(", ", arguments)})";
        if (type == "Workers.Socket" && name == "Connect") return $"{_imports.Require("cloudflare:sockets", "connect", "connectSocket")}({string.Join(", ", arguments)})";
        if (type == "Workers.Router" && method is not null) return RouterInvocation(invocation, method, receiver, arguments);
        if (type == "Workers.RouteContext" && name == "Parameter") return RouteParameterInvocation(receiver, arguments);
        if (type == "Workers.Cors" && name == "Allow") return HelperInvocation(JavaScriptHelper.Cors, arguments);
        if (type == "Workers.Cookies" && name is "Get" or "GetAll" or "Has")
            return $"{receiver}.{LowerFirst(name)}({arguments[0]})";
        if (type == "Workers.ResponseCookies" && name is "Set" or "Delete")
            return ResponseCookieInvocation(receiver, name, arguments);
        if (type == "Workers.Crypto") receiver = "globalThis.crypto";
        if (type == "Workers.SubtleCrypto" && method?.IsStatic == true) receiver = "globalThis.SubtleCrypto";
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic)) return EmitBindingIntrinsic(receiver, invocation, method, intrinsic);
        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return GeneratedSourceInvocation(method, receiver, arguments, invocation);
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

    private static readonly HashSet<string> EnvironmentBindings =
    [
        "Get", "Secret", "Raw", "KV", "R2", "D1", "Queue", "DurableObject", "Service", "Assets", "MtlsCertificate",
        "DispatchNamespace", "RateLimit", "AnalyticsEngine", "SendEmail", "VersionMetadata", "Ai", "Workflow", "Images",
        "Media", "Vectorize", "SecretsStoreSecret", "Hyperdrive"
    ];
}
