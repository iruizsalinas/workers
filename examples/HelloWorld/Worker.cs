using Workers;

namespace HelloWorld;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request)
    {
        if (new Url(request.Url).Pathname == "/")
            return Task.FromResult(new Response("Hello from C# on Cloudflare Workers."));

        return Task.FromResult(new Response("Not found") { Status = 404 });
    }
}
