using Workers;

namespace Redirects;

public static class Worker
{
    [FetchEvent]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var location = request.QueryParameters.Get("to") ?? "https://example.com";
        return Task.FromResult(Response.Redirect(location, status: 302));
    }
}
