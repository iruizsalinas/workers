namespace Workers;

public static class Cors
{
    /// <summary>Allows exact origins (or * without credentials). Defaults to GET, HEAD, POST and no request headers.
    /// Register before authentication middleware so preflights and error responses receive CORS headers.
    /// CORS controls browser access to responses; applications still enforce authentication and CSRF protection.</summary>
    public static Func<Request, RouteContext, Func<Task<Response>>, Task<Response>> Allow(
        string[] origins, string[]? methods = null, bool credentials = false,
        string[]? headers = null, string[]? exposedHeaders = null, int maxAge = 0) =>
        WorkerApi.NotExecutable<Func<Request, RouteContext, Func<Task<Response>>, Task<Response>>>();
}
