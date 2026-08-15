using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Expression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax value => Literal(value),
        IdentifierNameSyntax value => Identifier(value),
        ThisExpressionSyntax => "this",
        ParenthesizedExpressionSyntax value => $"({Expression(value.Expression)})",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.LogicalNotExpression) =>
            $"!{Expression(value.Operand)}",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryMinusExpression) =>
            $"-{Expression(value.Operand)}",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryPlusExpression) =>
            $"+{Expression(value.Operand)}",
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.SuppressNullableWarningExpression) =>
            Expression(value.Operand),
        AwaitExpressionSyntax value => $"await {Expression(value.Expression)}",
        BinaryExpressionSyntax value => Binary(value),
        ConditionalExpressionSyntax value => $"{Expression(value.Condition)} ? {Expression(value.WhenTrue)} : {Expression(value.WhenFalse)}",
        ConditionalAccessExpressionSyntax value => ConditionalAccess(value),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SimpleAssignmentExpression) => $"{Expression(value.Left)} = {Expression(value.Right)}",
        IsPatternExpressionSyntax value => IsPattern(value),
        MemberAccessExpressionSyntax value => Member(value),
        InvocationExpressionSyntax value => Invocation(value),
        ElementAccessExpressionSyntax value => ElementAccess(value),
        AnonymousObjectCreationExpressionSyntax value => "{ " + string.Join(", ", value.Initializers.Select(AnonymousMember)) + " }",
        InterpolatedStringExpressionSyntax value => "`" + string.Concat(value.Contents.Select(InterpolatedPart)) + "`",
        CollectionExpressionSyntax value => "[" + string.Join(", ", value.Elements.Select(Element)) + "]",
        ObjectCreationExpressionSyntax value => ObjectCreation(value),
        ImplicitObjectCreationExpressionSyntax value => ObjectCreation(value),
        _ => throw Unsupported("WRK101", expression)
    };

    private string Invocation(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments.Select(argument => Expression(argument.Expression)).ToArray();
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var containingType = method?.ContainingType.ToDisplayString();
        var methodName = method?.Name;
        if (containingType is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" && methodName == "FromResult") return arguments[0];
        if (containingType == "System.Threading.Tasks.Task" && methodName == "Delay")
        {
            if (arguments.Length != 1)
                throw UnsupportedSymbol(method, invocation);
            var helper = _helpers.Require(JavaScriptHelper.Delay);
            return $"{helper}({arguments[0]})";
        }
        if (containingType == "System.TimeSpan" && methodName == "FromMilliseconds") return arguments[0];
        if (containingType == "System.Console" && methodName == "WriteLine")
            return arguments.Length == 1 ? $"console.log({arguments[0]})" : throw UnsupportedSymbol(method, invocation);
        if (containingType == "System.Guid" && methodName == "NewGuid") return "globalThis.crypto.randomUUID()";
        if (containingType == "System.Uri" && methodName == "UnescapeDataString") return $"decodeURIComponent({arguments[0]})";
        if (containingType == "System.Convert" && methodName == "ToHexString")
            return $"Array.from({arguments[0]}, byte => byte.toString(16).padStart(2, \"0\")).join(\"\")";
        if (containingType == "Workers.Body" && methodName == "Text") return arguments[0];
        if (containingType == "Workers.Body" && methodName == "Json") return $"JSON.stringify({arguments[0]})";
        if (containingType == "Workers.Body" && methodName == "FromBytes") return arguments[0];
        if (containingType == "Workers.Response" && methodName == "Text") return Response(arguments, "text");
        if (containingType == "Workers.Response" && methodName == "Html")
        {
            var status = arguments.Length > 1 ? arguments[1] : "200";
            return $"new Response({arguments[0]}, {{ status: {status}, headers: {{ \"content-type\": \"text/html; charset=utf-8\" }} }})";
        }
        if (containingType == "Workers.Response" && methodName == "Json") return $"Response.json({arguments[0]}{ResponseInit(arguments, 1)})";
        if (containingType == "Workers.Response" && methodName == "Empty") return $"new Response(null{ResponseInit(arguments, 0)})";
        if (containingType == "Workers.Response" && methodName == "Redirect") return $"Response.redirect({arguments[0]}, {(arguments.Length > 1 ? arguments[1] : "302")})";
        if (containingType == "Workers.Response" && methodName == "FromBody") return $"new Response({arguments[0]}.body ?? {arguments[0]})";
        if (containingType == "Workers.Response" && methodName == "FromStream")
            return method?.Parameters.Length >= 2 && method.Parameters[1].Type.ToDisplayString() == "Workers.Headers"
                ? $"new Response({arguments[0]}, {{ status: {(arguments.Length > 2 ? arguments[2] : "200")}, headers: {arguments[1]} }})"
                : $"new Response({arguments[0]}{ResponseInit(arguments, 1)})";
        if (containingType == "Workers.Response" && methodName == "WebSocket") return $"new Response(null, {{ status: 101, webSocket: {arguments[0]} }})";
        if (containingType == "Workers.Response" && methodName == "WithHeader")
        {
            var helper = _helpers.Require(JavaScriptHelper.WithHeader);
            return $"{helper}({Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression)}, {arguments[0]}, {arguments[1]})";
        }
        if (containingType == "Workers.Response" && methodName == "AppendHeader")
        {
            var helper = _helpers.Require(JavaScriptHelper.WithHeader);
            return $"{helper}({Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression)}, {arguments[0]}, {arguments[1]}, \"append\")";
        }
        if (containingType == "Workers.Response" && methodName == "WithoutHeader")
        {
            var helper = _helpers.Require(JavaScriptHelper.WithHeader);
            return $"{helper}({Expression(((MemberAccessExpressionSyntax)invocation.Expression).Expression)}, {arguments[0]}, undefined, \"delete\")";
        }

        if (invocation.Expression is MemberAccessExpressionSyntax member)
        {
            var receiver = Expression(member.Expression);
            var name = member.Name.Identifier.Text;
            if (containingType == "System.IO.TextWriter" && name == "WriteLine"
                && _model.GetSymbolInfo(member.Expression).Symbol is IPropertySymbol { ContainingType: { } consoleType, Name: "Error" }
                && consoleType.ToDisplayString() == "System.Console")
                return arguments.Length == 1 ? $"console.error({arguments[0]})" : throw UnsupportedSymbol(method, invocation);
            if (TryEmitFrameworkInvocation(invocation, method, receiver, name, arguments, out var framework))
                return framework;
            if (containingType == "Workers.Env" && EnvironmentBindings.Contains(name))
                return $"{receiver}[{arguments[0]}]";
            if (containingType == "Workers.CacheStorage" && name == "OpenAsync")
                return $"caches.open({arguments[0]})";
            if (containingType == "Workers.Http" && name == "FetchAsync")
                return $"fetch({string.Join(", ", arguments)})";
            if (containingType == "Workers.WebSocketPair" && name == "Create")
                return "new WebSocketPair()";
            if (containingType == "Workers.TcpSocket" && name == "Connect")
            {
                var connect = _imports.Require("cloudflare:sockets", "connect", "connectSocket");
                if (method?.Parameters.Length >= 2
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_String
                    && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
                {
                    var options = arguments.Length > 2 ? $", {arguments[2]}" : "";
                    return $"{connect}({{ hostname: {arguments[0]}, port: {arguments[1]} }}{options})";
                }
                return $"{connect}({string.Join(", ", arguments)})";
            }
            if (containingType == "Workers.Crypto")
                receiver = "globalThis.crypto";
            if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic))
                return EmitBindingIntrinsic(receiver, invocation, method, intrinsic);
            if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
                return $"{receiver}.{LowerFirst(method.Name.Replace("Async", "", StringComparison.Ordinal))}({string.Join(", ", arguments)})";
            if (method is not null && method.DeclaringSyntaxReferences.Length != 0)
                return EmitUserInvocation(method, invocation, arguments);
            throw UnsupportedSymbol(method, invocation);
        }

        if (method is { IsStatic: false } && IsGeneratedInstanceType(method.ContainingType))
            return $"this.{LowerFirst(method.Name.Replace("Async", "", StringComparison.Ordinal))}({string.Join(", ", arguments)})";
        if (method is not null && method.DeclaringSyntaxReferences.Length != 0)
            return EmitUserInvocation(method, invocation, arguments);
        if (method is not null)
            throw UnsupportedSymbol(method, invocation);
        return $"{Expression(invocation.Expression)}({string.Join(", ", arguments)})";
    }

    private static readonly HashSet<string> EnvironmentBindings =
    [
        "Get", "Variable", "Secret", "Raw", "Kv", "R2", "Service", "Assets", "Mtls",
        "Dispatcher", "Queue", "D1", "DurableObject", "RateLimiter", "Analytics", "Email", "Version",
        "Ai", "Workflow", "Images", "Media", "Vectorize", "SecretStore", "Hyperdrive"
    ];
}
