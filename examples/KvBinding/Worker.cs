using Workers;

namespace KvBinding;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var key = request.QueryParameters.Get("key") ?? "message";
        var value = await environment.Kv("KV").GetTextAsync(key);

        return value is null
            ? Response.Text("Not found", status: 404)
            : Response.Text(value);
    }
}
