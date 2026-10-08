using System.Text.RegularExpressions;

// A Router pattern parsed when the Worker is built. Segments are literals, parameters such as {id} or
// {id:int}, or a final catch-all {*path}. Rank orders routes from most to least specific per segment, and
// Key identifies patterns that match exactly the same paths, whatever their parameter names.
internal sealed partial record RoutePattern(string Text, IReadOnlyList<RouteSegment> Segments)
{
    public static readonly IReadOnlySet<string> Constraints = new HashSet<string>(StringComparer.Ordinal) { "int", "guid" };

    public IEnumerable<string> ParameterNames => Segments.Select(segment => segment switch
    {
        RouteParameter parameter => parameter.Name,
        RouteCatchAll catchAll => catchAll.Name,
        _ => null
    }).OfType<string>();

    public IEnumerable<int> Rank => Segments.Select(segment => segment switch
    {
        RouteLiteral => 0,
        RouteParameter { Constraint: not null } => 1,
        RouteParameter => 2,
        _ => 3
    });

    public string Key => string.Concat(Segments.Select(segment => segment switch
    {
        RouteLiteral literal => "/" + literal.Text,
        RouteParameter parameter => "/{:" + parameter.Constraint + "}",
        _ => "/{*}"
    }));

    public static RoutePattern Parse(string text)
    {
        if (!text.StartsWith('/'))
            throw Invalid(text, "it must start with '/'");
        if (text == "/")
            return new RoutePattern(text, []);

        var parts = text[1..].Split('/');
        var segments = new List<RouteSegment>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0)
                throw Invalid(text, "it contains an empty segment; remove the trailing or repeated '/'");
            if (part.IndexOfAny(['{', '}']) < 0)
            {
                if (part.IndexOfAny(['?', '#']) >= 0)
                    throw Invalid(text, "it must not contain a query or fragment");
                segments.Add(new RouteLiteral(part));
                continue;
            }
            if (part[0] != '{' || part[^1] != '}' || part.AsSpan(1, part.Length - 2).IndexOfAny('{', '}') >= 0)
                throw Invalid(text, $"the segment '{part}' mixes text and a parameter; a parameter must fill a whole segment, such as '/users/{{id}}'");

            var body = part[1..^1];
            var catchAll = body.StartsWith('*');
            if (catchAll) body = body[1..];
            var separator = body.IndexOf(':');
            var name = separator < 0 ? body : body[..separator];
            var constraint = separator < 0 ? null : body[(separator + 1)..];
            if (name.EndsWith('?') || constraint?.EndsWith('?') == true)
                throw Invalid(text, "optional parameters are not supported; register a second route without the parameter");
            if (!ParameterName().IsMatch(name))
                throw Invalid(text, $"'{name}' is not a valid parameter name");
            if (!names.Add(name))
                throw Invalid(text, $"the parameter '{name}' appears more than once");
            if (catchAll)
            {
                if (constraint is not null)
                    throw Invalid(text, $"the catch-all parameter '{name}' cannot have a constraint");
                if (index != parts.Length - 1)
                    throw Invalid(text, $"the catch-all parameter '{name}' must be the last segment");
                segments.Add(new RouteCatchAll(name));
                continue;
            }
            if (constraint is not null && !Constraints.Contains(constraint))
                throw Invalid(text, $"the constraint '{constraint}' is not supported; use {string.Join(" or ", Constraints.Order().Select(item => $"'{item}'"))}");
            segments.Add(new RouteParameter(name, constraint));
        }
        return new RoutePattern(text, segments);
    }

    private static NotSupportedException Invalid(string text, string reason) =>
        new($"WRK122: The route pattern '{text}' is not valid because {reason}.");

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ParameterName();
}

internal abstract record RouteSegment;

internal sealed record RouteLiteral(string Text) : RouteSegment;

internal sealed record RouteParameter(string Name, string? Constraint) : RouteSegment;

internal sealed record RouteCatchAll(string Name) : RouteSegment;
