using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class AspNetExpressionEmitter
{
    private readonly SemanticModel _model;
    private readonly Dictionary<ISymbol, string> _names;

    public AspNetExpressionEmitter(SemanticModel model, IReadOnlyDictionary<ISymbol, string> parameters)
    {
        _model = model;
        _names = new Dictionary<ISymbol, string>(parameters, SymbolEqualityComparer.Default);
    }

    public void Register(VariableDeclaratorSyntax variable) =>
        _names[_model.GetDeclaredSymbol(variable)!] = $"v{_names.Count}";

    public string Name(VariableDeclaratorSyntax variable) =>
        _names[_model.GetDeclaredSymbol(variable)!];

    public string Response(ExpressionSyntax expression)
    {
        if (expression is AwaitExpressionSyntax awaited)
            expression = awaited.Expression;
        if (expression is ConditionalExpressionSyntax conditional)
            return $"{Value(conditional.Condition)} ? {Response(conditional.WhenTrue)} : {Response(conditional.WhenFalse)}";
        if (expression is InvocationExpressionSyntax invocation
            && TryResult(invocation, out var result))
            return result;
        var value = Value(expression);
        var type = _model.GetTypeInfo(expression).ConvertedType ?? _model.GetTypeInfo(expression).Type;
        if (type?.SpecialType == SpecialType.System_String)
            return $"new Response({value}, {{ headers: {{ \"content-type\": \"text/plain; charset=utf-8\" }} }})";
        return $"Response.json({value})";
    }

    public string Value(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => AspNetJavaScript.Literal(literal),
        IdentifierNameSyntax identifier => Identifier(identifier),
        ParenthesizedExpressionSyntax parenthesized => $"({Value(parenthesized.Expression)})",
        AwaitExpressionSyntax awaited => $"await {Value(awaited.Expression)}",
        AnonymousObjectCreationExpressionSyntax anonymous => "{ " + string.Join(", ", anonymous.Initializers.Select(Anonymous)) + " }",
        ObjectCreationExpressionSyntax creation => Object(creation),
        ImplicitObjectCreationExpressionSyntax creation => Object(creation),
        MemberAccessExpressionSyntax member => Member(member),
        ElementAccessExpressionSyntax element => Element(element),
        InvocationExpressionSyntax invocation => Invocation(invocation),
        InterpolatedStringExpressionSyntax interpolated => "`" + string.Concat(interpolated.Contents.Select(Interpolation)) + "`",
        BinaryExpressionSyntax binary => $"{Value(binary.Left)} {Operator(binary.Kind())} {Value(binary.Right)}",
        PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression) => $"!{Value(prefix.Operand)}",
        ConditionalExpressionSyntax conditional => $"{Value(conditional.Condition)} ? {Value(conditional.WhenTrue)} : {Value(conditional.WhenFalse)}",
        _ => throw AspNetDiagnostic.Unsupported("WRK207", expression, "This expression is not supported in ASP.NET handlers.")
    };

    private string Identifier(IdentifierNameSyntax identifier)
    {
        var symbol = _model.GetSymbolInfo(identifier).Symbol;
        if (symbol is not null && _names.TryGetValue(symbol, out var name)) return name;
        return identifier.Identifier.Text;
    }

    private string Anonymous(AnonymousObjectMemberDeclaratorSyntax member)
    {
        var name = member.NameEquals?.Name.Identifier.Text
            ?? (member.Expression as IdentifierNameSyntax)?.Identifier.Text
            ?? (member.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text
            ?? throw AspNetDiagnostic.Unsupported("WRK207", member, "Anonymous members require a name.");
        return $"{AspNetJavaScript.Property(name)}: {Value(member.Expression)}";
    }

    private string Object(BaseObjectCreationExpressionSyntax creation)
    {
        var constructor = _model.GetSymbolInfo(creation).Symbol as IMethodSymbol;
        if (constructor is null)
            throw AspNetDiagnostic.Unsupported("WRK207", creation, "The object constructor could not be resolved.");
        var arguments = creation.ArgumentList?.Arguments.Select(argument => Value(argument.Expression)).ToArray() ?? [];
        return "{ " + string.Join(", ", constructor.Parameters.Select((parameter, index) =>
            $"{AspNetJavaScript.Property(parameter.Name)}: {arguments[index]}")) + " }";
    }

    private string Invocation(InvocationExpressionSyntax invocation)
    {
        if (TryResult(invocation, out var result)) return result;
        var method = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments.Select(argument => Value(argument.Expression)).ToArray();
        return (method?.ContainingType.ToDisplayString(), method?.Name) switch
        {
            ("int", "Parse") => $"Number.parseInt({arguments[0]}, 10)",
            ("System.Guid", "NewGuid") => "globalThis.crypto.randomUUID()",
            ("System.String", "IsNullOrEmpty") => $"!{arguments[0]}",
            ("Microsoft.Extensions.Primitives.StringValues", "ToString") => Value(((MemberAccessExpressionSyntax)invocation.Expression).Expression),
            _ => throw AspNetDiagnostic.Unsupported("WRK208", invocation, $"Invocation '{method?.ToDisplayString()}' is not supported in ASP.NET handlers.")
        };
    }

    private string Member(MemberAccessExpressionSyntax member)
    {
        var property = _model.GetSymbolInfo(member).Symbol as IPropertySymbol;
        if (property?.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Http.HttpRequest")
        {
            var receiver = Value(member.Expression);
            return property.Name switch
            {
                "Method" => $"{receiver}.method",
                "Path" => "url.pathname",
                "QueryString" => "url.search",
                "Scheme" => "url.protocol.slice(0, -1)",
                "Host" => "url.host",
                "Headers" => $"{receiver}.headers",
                _ => throw AspNetDiagnostic.Unsupported("WRK207", member, $"HttpRequest.{property.Name} is not supported.")
            };
        }
        return $"{Value(member.Expression)}.{AspNetJavaScript.Property(member.Name.Identifier.Text)}";
    }

    private string Element(ElementAccessExpressionSyntax element)
    {
        if (element.ArgumentList.Arguments.Count != 1)
            throw AspNetDiagnostic.Unsupported("WRK207", element, "Only single-value indexing is supported.");
        var receiver = Value(element.Expression);
        var argument = Value(element.ArgumentList.Arguments[0].Expression);
        var type = _model.GetTypeInfo(element.Expression).Type?.ToDisplayString();
        return type == "Microsoft.AspNetCore.Http.IHeaderDictionary"
            ? $"({receiver}.get({argument}) ?? \"\")"
            : $"{receiver}[{argument}]";
    }

    private bool TryResult(InvocationExpressionSyntax invocation, out string result) =>
        AspNetResultEmitter.TryEmit(_model, invocation, Value, out result);

    private string Interpolation(InterpolatedStringContentSyntax content) => content switch
    {
        InterpolatedStringTextSyntax text => text.TextToken.ValueText.Replace("`", "\\`").Replace("${", "\\${"),
        InterpolationSyntax value => "${" + Value(value.Expression) + "}",
        _ => ""
    };

    private static string Operator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => "===",
        SyntaxKind.NotEqualsExpression => "!==",
        SyntaxKind.LogicalAndExpression => "&&",
        SyntaxKind.LogicalOrExpression => "||",
        SyntaxKind.AddExpression => "+",
        SyntaxKind.SubtractExpression => "-",
        SyntaxKind.MultiplyExpression => "*",
        SyntaxKind.DivideExpression => "/",
        SyntaxKind.GreaterThanExpression => ">",
        SyntaxKind.GreaterThanOrEqualExpression => ">=",
        SyntaxKind.LessThanExpression => "<",
        SyntaxKind.LessThanOrEqualExpression => "<=",
        _ => throw new InvalidOperationException($"WRK207: Binary operator '{kind}' is not supported.")
    };
}
