using Workers;

namespace Redirects;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env env,
        ExecutionContext ctx)
    {
        var location = new Url(request.Url).SearchParams.Get("to") ?? "https://example.com";
        return Task.FromResult(Response.Redirect(location, status: 302));
    }
}
