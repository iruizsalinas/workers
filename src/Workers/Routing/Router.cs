namespace Workers;

/// <summary>Handles a matched route.</summary>
public delegate Task<Response> RouteHandler(Request request, RouteContext context);

/// <summary>A small Worker-native router supporting literal, parameter, and trailing wildcard segments.</summary>
public sealed class Router
{
    private readonly List<Route> _routes = [];
    private object? _data;
    private RouteHandler? _notFound;
    private RouteHandler? _methodNotAllowed;

    /// <summary>Adds a GET route.</summary>
    public Router Get(string pattern, RouteHandler handler) => Add("GET", pattern, handler);

    /// <summary>Adds a HEAD route.</summary>
    public Router Head(string pattern, RouteHandler handler) => Add("HEAD", pattern, handler);

    /// <summary>Adds an OPTIONS route.</summary>
    public Router Options(string pattern, RouteHandler handler) => Add("OPTIONS", pattern, handler);

    /// <summary>Adds a POST route.</summary>
    public Router Post(string pattern, RouteHandler handler) => Add("POST", pattern, handler);

    /// <summary>Adds a PUT route.</summary>
    public Router Put(string pattern, RouteHandler handler) => Add("PUT", pattern, handler);

    /// <summary>Adds a PATCH route.</summary>
    public Router Patch(string pattern, RouteHandler handler) => Add("PATCH", pattern, handler);

    /// <summary>Adds a DELETE route.</summary>
    public Router Delete(string pattern, RouteHandler handler) => Add("DELETE", pattern, handler);

    /// <summary>Adds a route that accepts any HTTP method.</summary>
    public Router All(string pattern, RouteHandler handler) => Add("*", pattern, handler);

    /// <summary>Adds a route for a custom HTTP method.</summary>
    public Router Method(string method, string pattern, RouteHandler handler) => Add(method, pattern, handler);

    /// <summary>Sets application data made available to route handlers.</summary>
    public Router WithData<T>(T data)
        where T : notnull
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        return this;
    }

    /// <summary>Sets the handler used when no route path matches.</summary>
    public Router NotFound(RouteHandler handler)
    {
        _notFound = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>Sets the handler used when a route path matches but the method does not.</summary>
    public Router MethodNotAllowed(RouteHandler handler)
    {
        _methodNotAllowed = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>Runs the router against the provided request.</summary>
    public async Task<Response> RunAsync(
        Request request,
        Env environment,
        Context? executionContext = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(environment);

        var allowedMethods = new List<string>();
        Dictionary<string, string>? fallbackParameters = null;
        foreach (var route in _routes)
        {
            if (!route.TryMatch(request.Path, out var parameters))
                continue;

            if (route.Method != "*" && !string.Equals(route.Method, request.Method, StringComparison.Ordinal))
            {
                if (!allowedMethods.Contains(route.Method, StringComparer.Ordinal))
                    allowedMethods.Add(route.Method);

                fallbackParameters ??= parameters;
                continue;
            }

            var context = CreateContext(environment, executionContext, parameters);
            return await route.Handler(request, context);
        }

        if (allowedMethods.Count > 0)
        {
            var context = CreateContext(environment, executionContext, fallbackParameters ?? EmptyParameters(), allowedMethods);
            if (_methodNotAllowed is not null)
                return await _methodNotAllowed(request, context);

            return Response.Error("Method Not Allowed", 405)
                .WithHeader("allow", string.Join(", ", allowedMethods));
        }

        if (_notFound is not null)
        {
            var context = CreateContext(environment, executionContext, EmptyParameters());
            return await _notFound(request, context);
        }

        return Response.Error("Not Found", 404);
    }

    private Router Add(string method, string pattern, RouteHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(handler);

        _routes.Add(new Route(NormalizeMethod(method), RoutePattern.Parse(pattern), handler));
        return this;
    }

    private RouteContext CreateContext(
        Env environment,
        Context? executionContext,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyList<string>? allowedMethods = null) =>
        new(
            environment,
            executionContext ?? new Context(),
            new RouteParameters(parameters),
            allowedMethods,
            _data);

    private static IReadOnlyDictionary<string, string> EmptyParameters() =>
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static string NormalizeMethod(string method) =>
        method == "*" ? "*" : method.ToUpperInvariant();

    private sealed record Route(string Method, RoutePattern Pattern, RouteHandler Handler)
    {
        public bool TryMatch(string path, out Dictionary<string, string> parameters) =>
            Pattern.TryMatch(path, out parameters);
    }
}
