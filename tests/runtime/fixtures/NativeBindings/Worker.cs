using Workers;

namespace NativeBindings;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/durable")
        {
            var stub = environment.DurableObject("ECHO").GetByName("generated-csharp");
            return await stub.FetchAsync("https://worker.test/from-csharp");
        }

        var database = environment.D1("DB");
        await database.ExecAsync("CREATE TABLE IF NOT EXISTS people (name TEXT NOT NULL); DELETE FROM people;");
        await database.Prepare("INSERT INTO people (name) VALUES (?)").Bind("Ada").RunAsync();
        var name = await database.Prepare("SELECT name FROM people LIMIT 1").FirstAsync<string>("name");
        return Response.Json(new { name });
    }
}

[DurableObject("EchoObject")]
public sealed class EchoObject
{
    private readonly DurableObjectState _state;

    public EchoObject(DurableObjectState state, Env environment)
    {
        _state = state;
    }

    public Response FetchAsync(Request request) =>
        Response.Text($"durable:{request.Path}");

    public string Greet(string name) => $"Hello, {name}";

    public async Task<string> StoreAsync(string value)
    {
        await _state.Storage.PutAsync("rpc-value", value);
        await _state.Storage.PutAsync("count", 0);
        return await _state.Storage.GetAsync<string>("rpc-value") ?? "missing";
    }

    public async Task<int> IncrementAsync()
    {
        var current = await _state.Storage.GetAsync<int>("count");
        var next = current + 1;
        await _state.Storage.PutAsync("count", next);
        return next;
    }

    public void AlarmAsync(AlarmInfo info)
    {
    }
}
