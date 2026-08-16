using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AspNetApplicationDiscovery
{
    private static readonly Dictionary<string, string> MapMethods = new(StringComparer.Ordinal)
    {
        ["MapGet"] = "GET",
        ["MapPost"] = "POST",
        ["MapPut"] = "PUT",
        ["MapDelete"] = "DELETE",
        ["MapPatch"] = "PATCH"
    };

    public static bool TryDiscover(CSharpCompilation compilation, out AspNetApplication application)
    {
        var endpoints = new List<AspNetEndpoint>();
        var foundAspNet = false;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                    continue;
                if (IsUnsupportedPipelineCall(method, invocation, model))
                    throw AspNetDiagnostic.Unsupported(
                        "WRK203",
                        invocation,
                        $"ASP.NET pipeline operation '{method.Name}' is not supported by the native Worker frontend.");
                if (!IsAspNetBuilderMethod(method))
                    continue;

                foundAspNet = true;
                if (!MapMethods.TryGetValue(method.Name, out var httpMethod))
                    continue;
                endpoints.Add(ReadEndpoint(invocation, model, httpMethod));
            }
        }

        application = new AspNetApplication(compilation, endpoints);
        if (!foundAspNet)
            return false;
        if (endpoints.Count == 0)
            throw new InvalidOperationException("WRK200: The ASP.NET application does not map any supported endpoints.");
        return true;
    }

    private static AspNetEndpoint ReadEndpoint(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        string httpMethod)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2 || model.GetConstantValue(arguments[0].Expression) is not { HasValue: true, Value: string pattern })
            throw AspNetDiagnostic.Unsupported("WRK201", invocation, "Endpoint routes must be constant strings.");
        return ReadHandler(arguments[1].Expression, model, httpMethod, pattern);
    }

    private static AspNetEndpoint ReadHandler(
        ExpressionSyntax expression,
        SemanticModel model,
        string httpMethod,
        string pattern) => expression switch
        {
            ParenthesizedLambdaExpressionSyntax lambda => new(
                httpMethod, pattern, lambda.ParameterList.Parameters.ToArray(), lambda.ExpressionBody, lambda.Block, model),
            SimpleLambdaExpressionSyntax lambda => new(
                httpMethod, pattern, [lambda.Parameter], lambda.ExpressionBody, lambda.Block, model),
            _ when ResolveMethod(model, expression) is { } method
                   && method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax declaration => new(
                httpMethod,
                pattern,
                declaration.ParameterList.Parameters.ToArray(),
                declaration.ExpressionBody?.Expression,
                declaration.Body,
                model.Compilation.GetSemanticModel(declaration.SyntaxTree)),
            _ when ResolveMethod(model, expression) is { } method
                   && method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is LocalFunctionStatementSyntax declaration => new(
                httpMethod,
                pattern,
                declaration.ParameterList.Parameters.ToArray(),
                declaration.ExpressionBody?.Expression,
                declaration.Body,
                model.Compilation.GetSemanticModel(declaration.SyntaxTree)),
            _ => throw AspNetDiagnostic.Unsupported("WRK202", expression, "Endpoint handlers must be lambdas or source-declared static methods.")
        };

    private static IMethodSymbol? ResolveMethod(SemanticModel model, ExpressionSyntax expression)
    {
        var info = model.GetSymbolInfo(expression);
        return info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
    }

    private static bool IsAspNetBuilderMethod(IMethodSymbol method) =>
        method.Name is ("CreateBuilder" or "CreateSlimBuilder" or "Build" or "Run" or "RunAsync"
            or "MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch")
        && method.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal);

    private static bool IsUnsupportedPipelineCall(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel model)
    {
        var scope = method.ContainingNamespace.ToDisplayString();
        if (scope.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
            return true;
        if (!scope.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            return false;
        if (IsAspNetBuilderMethod(method)) return false;
        var receiverExpression = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
        var receiver = receiverExpression is null ? null : model.GetTypeInfo(receiverExpression).Type?.ToDisplayString();
        return receiver is "Microsoft.AspNetCore.Builder.WebApplication"
            or "Microsoft.AspNetCore.Builder.IApplicationBuilder";
    }
}
