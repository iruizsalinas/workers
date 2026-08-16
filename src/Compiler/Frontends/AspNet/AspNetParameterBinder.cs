using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class AspNetParameterBinder(AspNetEndpoint endpoint)
{
    private readonly AspNetRoute _route = AspNetRoute.Parse(endpoint.Pattern);

    public string RegexLiteral => $"/{_route.Regex.Replace("/", "\\/")}/";

    public IReadOnlyDictionary<ISymbol, string> Bind(
        IEnumerable<ParameterSyntax> parameters,
        IList<string> statements)
    {
        var bindings = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var bodyBound = false;
        foreach (var parameter in parameters)
        {
            var symbol = (IParameterSymbol)endpoint.SemanticModel.GetDeclaredSymbol(parameter)!;
            var name = $"p{bindings.Count}";
            var source = Source(symbol);
            var routeName = source.Name ?? symbol.Name;
            var routeParameter = _route.Parameters.FirstOrDefault(value =>
                string.Equals(value.Name, routeName, StringComparison.OrdinalIgnoreCase));
            var value = source.Kind switch
            {
                ParameterSource.Route when routeParameter is null =>
                    throw AspNetDiagnostic.Unsupported("WRK211", parameter, $"Route parameter '{routeName}' does not exist."),
                ParameterSource.Route => $"match[{routeParameter!.Group}]",
                ParameterSource.Query => $"url.searchParams.get({AspNetJavaScript.String(source.Name ?? symbol.Name)})",
                ParameterSource.Header => $"request.headers.get({AspNetJavaScript.String(source.Name ?? symbol.Name)})",
                ParameterSource.Body => BindBody(ref bodyBound, parameter),
                _ when routeParameter is not null => $"match[{routeParameter.Group}]",
                _ => BindNonRoute(symbol, ref bodyBound)
            };
            if (symbol.HasExplicitDefaultValue)
                value = $"({value} ?? {DefaultValue(symbol.ExplicitDefaultValue)})";
            if (RequiresValue(symbol))
                statements.Add($"  if ({value} == null) return Response.json({{ title: \"Bad Request\", status: 400, detail: {AspNetJavaScript.String($"Missing required parameter '{source.Name ?? symbol.Name}'.")} }}, {{ status: 400 }});");
            statements.Add($"  const {name} = {Convert(value, symbol.Type)};");
            bindings.Add(symbol, name);
        }
        return bindings;
    }

    private static string BindNonRoute(IParameterSymbol parameter, ref bool bodyBound)
    {
        var type = parameter.Type.ToDisplayString();
        if (type == "Microsoft.AspNetCore.Http.HttpRequest") return "request";
        if (type == "System.Threading.CancellationToken") return "request.signal";
        if (IsSimple(parameter.Type))
            return $"url.searchParams.get({AspNetJavaScript.String(parameter.Name)})";
        if (bodyBound)
            throw AspNetDiagnostic.Unsupported("WRK204", parameter.DeclaringSyntaxReferences[0].GetSyntax(), "Only one request body parameter is supported.");
        bodyBound = true;
        return "await request.json()";
    }

    private static string BindBody(ref bool bodyBound, SyntaxNode source)
    {
        if (bodyBound)
            throw AspNetDiagnostic.Unsupported("WRK204", source, "Only one request body parameter is supported.");
        bodyBound = true;
        return "await request.json()";
    }

    private static string Convert(string value, ITypeSymbol type)
    {
        var nullable = type.NullableAnnotation == NullableAnnotation.Annotated;
        var actual = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
            ? named.TypeArguments[0]
            : type;
        var converted = actual.SpecialType switch
        {
            SpecialType.System_String => value,
            SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64
                or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 => $"Number.parseInt({value}, 10)",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => $"Number({value})",
            SpecialType.System_Boolean => $"{value} === \"true\"",
            _ => value
        };
        return converted;
    }

    private static bool RequiresValue(IParameterSymbol parameter) =>
        !parameter.HasExplicitDefaultValue
        && parameter.NullableAnnotation != NullableAnnotation.Annotated
        && parameter.Type is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
        && parameter.Type.ToDisplayString() is not "Microsoft.AspNetCore.Http.HttpRequest" and not "System.Threading.CancellationToken"
        && IsSimple(parameter.Type);

    private static ParameterSource Source(IParameterSymbol parameter)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            var kind = attribute.AttributeClass?.ToDisplayString() switch
            {
                "Microsoft.AspNetCore.Mvc.FromRouteAttribute" => ParameterSource.Route,
                "Microsoft.AspNetCore.Mvc.FromQueryAttribute" => ParameterSource.Query,
                "Microsoft.AspNetCore.Mvc.FromHeaderAttribute" => ParameterSource.Header,
                "Microsoft.AspNetCore.Mvc.FromBodyAttribute" => ParameterSource.Body,
                _ => ParameterSource.Inferred
            };
            if (kind == ParameterSource.Inferred) continue;
            var name = attribute.NamedArguments.FirstOrDefault(item => item.Key == "Name").Value.Value as string;
            return new ParameterSource(kind, name);
        }
        return new ParameterSource(ParameterSource.Inferred, null);
    }

    private static bool IsSimple(ITypeSymbol type) => type.SpecialType is
        SpecialType.System_String or SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64
        or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64
        or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_Boolean;

    private static string DefaultValue(object? value) => value switch
    {
        null => "null",
        string text => AspNetJavaScript.String(text),
        bool boolean => boolean ? "true" : "false",
        IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("WRK212: This endpoint parameter default value is not supported.")
    };
}

internal readonly record struct ParameterSource(int Kind, string? Name)
{
    public const int Inferred = 0;
    public const int Route = 1;
    public const int Query = 2;
    public const int Header = 3;
    public const int Body = 4;
}
