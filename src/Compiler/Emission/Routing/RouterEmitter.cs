using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Router patterns are compile-time constants parsed when the Worker is built and emitted once as
// module-level route descriptors. The router itself is a small runtime table: registration keeps routes
// ordered by specificity, and dispatch passes the native Request with a RouteContext of env, context and
// the decoded path parameters.
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<string, (string Name, RoutePattern Pattern)> _routes = new(StringComparer.Ordinal);
    private readonly StringBuilder _routeDeclarations = new();

    private static readonly Dictionary<string, string?> RouterMethods = new(StringComparer.Ordinal)
    {
        ["Get"] = "GET",
        ["Post"] = "POST",
        ["Put"] = "PUT",
        ["Patch"] = "PATCH",
        ["Delete"] = "DELETE",
        ["Options"] = "OPTIONS",
        ["Any"] = null
    };

    private string CreateRouter() => $"{_helpers.Require(JavaScriptHelper.Router)}()";

    private string RouterInvocation(InvocationExpressionSyntax invocation, IMethodSymbol method, string receiver, string[] arguments)
    {
        _helpers.Require(JavaScriptHelper.Router);
        var sourceArguments = invocation.ArgumentList.Arguments;
        if (method.Name == "HandleAsync")
            return $"{_helpers.Name("routerHandle")}({receiver}, {string.Join(", ", arguments)})";
        if (method.Name == "Fallback")
        {
            ValidateRouteHandler(NamedArgument(method, sourceArguments, "handler")!, null);
            return $"{_helpers.Name("routerFallback")}({receiver}, {arguments[0]})";
        }
        if (!RouterMethods.TryGetValue(method.Name, out var httpMethod))
            throw UnsupportedSymbol(method, invocation);

        var patternSyntax = NamedArgument(method, sourceArguments, "pattern")!;
        if (_model.GetConstantValue(patternSyntax) is not { HasValue: true, Value: string text })
            throw Locate(new NotSupportedException(
                "WRK122: Route patterns must be compile-time constants so they can be checked when the Worker is built."), patternSyntax);
        var (route, pattern) = RouteConstant(text, patternSyntax);
        ValidateRouteHandler(NamedArgument(method, sourceArguments, "handler")!, pattern);
        var methodText = httpMethod is null ? "null" : JsonSerializer.Serialize(httpMethod);
        return $"{_helpers.Name("routerAdd")}({receiver}, {methodText}, {route}, {arguments[1]})";
    }

    private string RouteParameterInvocation(string receiver, string[] arguments)
    {
        _helpers.Require(JavaScriptHelper.Router);
        return $"{_helpers.Name("routerParameter")}({receiver}, {arguments[0]})";
    }

    private (string Name, RoutePattern Pattern) RouteConstant(string text, SyntaxNode source)
    {
        if (_routes.TryGetValue(text, out var existing)) return existing;
        RoutePattern pattern;
        try
        {
            pattern = RoutePattern.Parse(text);
        }
        catch (NotSupportedException exception)
        {
            throw Locate(exception, source);
        }

        var segments = pattern.Segments.Select(segment => segment switch
        {
            RouteLiteral literal => JsonSerializer.Serialize(literal.Text),
            RouteParameter { Constraint: { } constraint } parameter =>
                $"{{ name: {JsonSerializer.Serialize(parameter.Name)}, test: {RouteConstraintTest(constraint)} }}",
            RouteParameter parameter => $"{{ name: {JsonSerializer.Serialize(parameter.Name)} }}",
            RouteCatchAll catchAll => $"{{ name: {JsonSerializer.Serialize(catchAll.Name)}, rest: true }}",
            _ => throw new InvalidOperationException()
        });
        var name = _names.Get("route:" + text, "route");
        _routeDeclarations.Append("const ").Append(name).Append(" = { pattern: ")
            .Append(JsonSerializer.Serialize(text)).Append(", key: ")
            .Append(JsonSerializer.Serialize(pattern.Key)).Append(", rank: [")
            .Append(string.Join(", ", pattern.Rank)).Append("], segments: [")
            .Append(string.Join(", ", segments)).AppendLine("] };");
        var result = (name, pattern);
        _routes.Add(text, result);
        return result;
    }

    // A constrained parameter matches exactly the values that int.Parse or Guid.Parse accept, so handlers can
    // parse it without handling a format error.
    private string RouteConstraintTest(string constraint)
    {
        var parse = constraint switch
        {
            "int" => NumericParse("value", 0),
            "guid" => HelperInvocation(JavaScriptHelper.GuidParse, ["value"]),
            _ => throw new InvalidOperationException()
        };
        return $"(value) => {{ try {{ {parse}; return true; }} catch {{ return false; }} }}";
    }

    // Reports RouteContext.Parameter calls with a constant name the route does not define. The check covers
    // lambdas and method groups whose RouteContext parameter is used directly; other uses are checked at runtime.
    private void ValidateRouteHandler(ExpressionSyntax handler, RoutePattern? pattern)
    {
        var (parameter, body, model) = RouteHandlerBody(handler);
        if (parameter is null || body is null) return;
        var names = pattern?.ParameterNames.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var call in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol { Name: "Parameter" } method
                || method.ContainingType.ToDisplayString() != "Workers.RouteContext"
                || call.Expression is not MemberAccessExpressionSyntax access
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Expression).Symbol, parameter)
                || call.ArgumentList.Arguments is not [{ Expression: var argument }]
                || model.GetConstantValue(argument) is not { HasValue: true, Value: string name }
                || names.Contains(name))
                continue;
            var message = pattern is null
                ? $"WRK122: The fallback handler has no route parameters, so it cannot read '{name}'."
                : $"WRK122: The route '{pattern.Text}' has no parameter named '{name}'"
                  + (names.Count == 0 ? "." : $"; it defines {string.Join(", ", names.Select(item => $"'{item}'"))}.");
            throw Locate(new NotSupportedException(message), argument);
        }
    }

    private (IParameterSymbol? Parameter, SyntaxNode? Body, SemanticModel Model) RouteHandlerBody(ExpressionSyntax handler)
    {
        if (handler is AnonymousFunctionExpressionSyntax lambda
            && _model.GetSymbolInfo(lambda).Symbol is IMethodSymbol { Parameters.Length: 2 } function)
            return (function.Parameters[1], lambda.Body, _model);
        if (_model.GetSymbolInfo(handler).Symbol is IMethodSymbol { Parameters.Length: 2 } method
            && method.DeclaringSyntaxReferences is [{ } reference]
            && reference.GetSyntax() is MethodDeclarationSyntax declaration
            && (declaration.Body ?? (SyntaxNode?)declaration.ExpressionBody) is { } body)
            return (method.Parameters[1], body, _compilation.GetSemanticModel(declaration.SyntaxTree));
        return (null, null, _model);
    }
}
