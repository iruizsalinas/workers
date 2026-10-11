using Workers;

namespace JsonApi;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env env,
        ExecutionContext ctx)
    {
        return Task.FromResult(Response.Json(new
        {
            ok = true,
            path = new Url(request.Url).Pathname
        }));
    }
}
