using Workers;

namespace KvBinding;

public static class Worker
{
    [FetchEvent]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var key = request.QueryParameters.Get("key") ?? "message";
        var value = await environment.Kv("KV").GetTextAsync(key);

        return value is null
            ? Response.Error("Not found", 404)
            : Response.Text(value);
    }
}
