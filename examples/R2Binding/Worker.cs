using Workers;

namespace R2Binding;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var key = request.QueryParameters.Get("key") ?? "hello.txt";
        var item = await environment.R2("BUCKET").GetAsync(key);
        if (item is null)
            return new Response("Object not found") { Status = 404 };

        var headers = new Headers();
        item.WriteHttpMetadata(headers);
        headers.Set("etag", item.HttpEtag);
        return new Response(item.Body) { Headers = headers };
    }
}
