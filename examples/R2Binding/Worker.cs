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
        var body = await environment.R2("BUCKET").GetAsync(key);

        return body is null
            ? Response.Text("Object not found", status: 404)
            : Response.FromBody(body);
    }
}
