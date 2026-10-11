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
            return Response.Text("Object not found", status: 404);

        var headers = new Headers();
        item.WriteHttpMetadata(headers);
        return Response.FromStream(item.Body, headers.Set("etag", item.HttpEtag));
    }
}
