using Workers;

namespace HelloWorld;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/")
            return Task.FromResult(Response.Text("Hello from C# on Cloudflare Workers."));

        return Task.FromResult(Response.Text("Not found", status: 404));
    }
}
