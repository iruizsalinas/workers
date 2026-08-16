using System.Text;

internal sealed class AspNetEndpointEmitter
{
    private readonly AspNetEndpoint _endpoint;
    private readonly int _index;

    public AspNetEndpointEmitter(AspNetEndpoint endpoint, int index) =>
        (_endpoint, _index) = (endpoint, index);

    public EmittedAspNetEndpoint Emit()
    {
        var name = $"endpoint{_index}";
        var binder = new AspNetParameterBinder(_endpoint);
        var statements = new List<string>();
        var bindings = binder.Bind(_endpoint.Parameters, statements);
        var expressions = new AspNetExpressionEmitter(_endpoint.SemanticModel, bindings);
        var body = new AspNetHandlerBodyEmitter(expressions).Emit(_endpoint.ExpressionBody, _endpoint.Block);

        var output = new StringBuilder();
        output.Append("async function ").Append(name).AppendLine("(request, env, ctx, url, match) {");
        foreach (var statement in statements) output.AppendLine(statement);
        output.Append(body);
        output.AppendLine("}").AppendLine();
        return new EmittedAspNetEndpoint(name, _endpoint.HttpMethod, binder.RegexLiteral, output.ToString());
    }
}
