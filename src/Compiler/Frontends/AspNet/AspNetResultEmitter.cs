using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AspNetResultEmitter
{
    public static bool TryEmit(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        Func<ExpressionSyntax, string> emit,
        out string result)
    {
        result = "";
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.ContainingType.ToDisplayString() is not ("Microsoft.AspNetCore.Http.Results" or "Microsoft.AspNetCore.Http.TypedResults"))
            return false;

        var arguments = invocation.ArgumentList.Arguments.Select(argument => emit(argument.Expression)).ToArray();
        result = method.Name switch
        {
            "Ok" => Json(arguments.FirstOrDefault(), 200),
            "Json" => Json(arguments.FirstOrDefault(), Status(invocation, method, 200)),
            "Created" => Created(arguments),
            "Accepted" => Json(arguments.ElementAtOrDefault(1), 202),
            "BadRequest" => Json(arguments.FirstOrDefault(), 400),
            "NotFound" => Json(arguments.FirstOrDefault(), 404),
            "Conflict" => Json(arguments.FirstOrDefault(), 409),
            "UnprocessableEntity" => Json(arguments.FirstOrDefault(), 422),
            "NoContent" => "new Response(null, { status: 204 })",
            "Unauthorized" => "new Response(null, { status: 401 })",
            "Forbid" => "new Response(null, { status: 403 })",
            "Text" or "Content" => Text(arguments),
            "Redirect" => $"Response.redirect({arguments[0]}, {Status(invocation, method, 302)})",
            "Problem" => Problem(arguments),
            _ => throw AspNetDiagnostic.Unsupported("WRK209", invocation, $"Result helper '{method.Name}' is not supported.")
        };
        return true;
    }

    private static string Json(string? value, int status) => value is null
        ? $"new Response(null, {{ status: {status} }})"
        : $"Response.json({value}, {{ status: {status} }})";

    private static string Created(string[] arguments)
    {
        var body = arguments.Length > 1 ? arguments[1] : "null";
        return $"Response.json({body}, {{ status: 201, headers: {{ location: {arguments[0]} }} }})";
    }

    private static string Text(string[] arguments) =>
        $"new Response({arguments[0]}, {{ headers: {{ \"content-type\": \"text/plain; charset=utf-8\" }} }})";

    private static string Problem(string[] arguments)
    {
        var detail = arguments.Length == 0 ? "null" : arguments[0];
        return $"Response.json({{ detail: {detail}, status: 500 }}, {{ status: 500, headers: {{ \"content-type\": \"application/problem+json\" }} }})";
    }

    private static int Status(InvocationExpressionSyntax invocation, IMethodSymbol method, int fallback)
    {
        for (var argumentIndex = 0; argumentIndex < invocation.ArgumentList.Arguments.Count; argumentIndex++)
        {
            var argument = invocation.ArgumentList.Arguments[argumentIndex];
            var parameterName = argument.NameColon?.Name.Identifier.Text
                ?? (argumentIndex < method.Parameters.Length ? method.Parameters[argumentIndex].Name : null);
            if (parameterName is not ("statusCode" or "permanent")) continue;
            var expression = argument.Expression;
            if (expression is LiteralExpressionSyntax literal && literal.Token.Value is int value) return value;
            if (expression is LiteralExpressionSyntax boolean && boolean.Token.Value is bool permanent)
                return permanent ? 301 : 302;
        }
        return fallback;
    }
}
