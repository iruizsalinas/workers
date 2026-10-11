namespace Workers;

public sealed class Router
{
    public Router() { }

    /// <summary>Runs in registration order, with responses unwinding in reverse order. Call next at most once.</summary>
    public Router Use(Func<Request, RouteContext, Func<Task<Response>>, Task<Response>> middleware) => WorkerApi.NotExecutable<Router>();
    /// <summary>Overrides empty default error responses, including middleware failures. Unexpected failures have a safe 500 message.</summary>
    public Router OnError(Func<RouterError, Request, RouteContext, Response> handler) => WorkerApi.NotExecutable<Router>();
    public Router OnError(Func<RouterError, Request, RouteContext, Task<Response>> handler) => WorkerApi.NotExecutable<Router>();

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

    public Task<Response> HandleAsync(Request request, Env env, ExecutionContext ctx) => WorkerApi.NotExecutable<Task<Response>>();
}

public sealed class RouteContext
{
    private RouteContext() { }

    public Env Env => WorkerApi.NotExecutable<Env>();
    public ExecutionContext Ctx => WorkerApi.NotExecutable<ExecutionContext>();

    public string Parameter(string name) => WorkerApi.NotExecutable<string>();
}
