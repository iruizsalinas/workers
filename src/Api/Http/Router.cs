namespace Workers;

public sealed class Router
{
    public Router() { }

    public Router Get(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Get(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Post(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Post(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Put(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Put(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Patch(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Patch(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Delete(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Delete(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Options(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Options(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Any(string pattern, Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Any(string pattern, Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();
    public Router Fallback(Func<Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router Fallback(Func<Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();

    public Task<Response> HandleAsync(Request request, Env env, Context context) => WorkerApi.NotExecutable<Task<Response>>();
}

public sealed class RouteContext
{
    public Env Env => WorkerApi.NotExecutable<Env>();
    public Context Context => WorkerApi.NotExecutable<Context>();

    public string Parameter(string name) => WorkerApi.NotExecutable<string>();
}
