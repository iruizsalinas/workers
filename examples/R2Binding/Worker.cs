using Workers;

namespace R2Binding;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env env)
    {
        var key = new Url(request.Url).SearchParams.Get("key") ?? "hello.txt";
        var item = await env.R2("BUCKET").GetAsync(key);
        if (item is null)
            return new Response("Object not found") { Status = 404 };

        var headers = new Headers();
        item.WriteHttpMetadata(headers);
        headers.Set("etag", item.HttpEtag);
        return new Response(item.Body) { Headers = headers };
    }
}
