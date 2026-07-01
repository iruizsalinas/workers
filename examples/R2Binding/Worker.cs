using Workers;

namespace R2Binding;

public static class Worker
{
    [FetchEvent]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var key = request.QueryParameters.Get("key") ?? "hello.txt";
        var body = await environment.Bucket("BUCKET").GetAsync(key);

        return body is null
            ? Response.Error("Object not found", 404)
            : Response.FromBody(body);
    }
}
