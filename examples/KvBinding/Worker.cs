using Workers;

namespace KvBinding;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env env)
    {
        var key = new Url(request.Url).SearchParams.Get("key") ?? "message";
        var value = await env.KV("KV").GetAsync(key);

        return value is null
            ? new Response("Not found") { Status = 404 }
            : new Response(value);
    }
}
