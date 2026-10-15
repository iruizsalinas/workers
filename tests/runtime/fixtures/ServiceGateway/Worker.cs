using Workers;

namespace ServiceGateway;

public static class Gateway
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        var url = new Url(request.Url);
        var path = url.Pathname;
        if (request.Method == "GET" && path == "/health")
        {
            var rpc = await environment.Service("CORE").InvokeAsync<Health>("health");
            var http = await environment.Service("CORE").FetchAsync("https://service/internal/health");
            return Response.Json(new { rpc, http = await http.JsonAsync<Health>() });
        }
        if (request.Method == "POST" && path == "/users")
        {
            var input = await request.JsonAsync<CreateUserInput>();
            var result = await environment.Service("ADMIN").InvokeAsync<CreateUserResult>("createUser", input);
            return result!.Error is null
                ? Response.Json(result.User, status: 201)
                : Response.Json(new { error = result.Error }, status: 400);
        }
        if (request.Method == "GET" && path == "/users")
        {
            var prefix = url.SearchParams.Get("prefix") ?? "";
            var users = await environment.Service("USERS").InvokeAsync<IReadOnlyList<User>>("searchUsers", prefix);
            return Response.Json(new { users });
        }
        if (request.Method == "GET" && path.StartsWith("/users/"))
        {
            var id = path.Substring(7);
            var user = await environment.Service("USERS").InvokeAsync<User>("getUser", id);
            return user is null ? Response.Json(new { error = "Not found" }, status: 404) : Response.Json(user);
        }
        if (request.Method == "GET" && path.StartsWith("/assets/"))
        {
            var asset = await environment.Service("USERS").InvokeAsync<Response>("getAsset", path.Substring(7));
            return asset is null ? new Response("Missing asset") { Status = 502 } : new Response(asset.Body, asset) { Headers = { ["x-served-via"] = "rpc" } };
        }
        return Response.Json(new { error = "Not found" }, status: 404);
    }
}

public sealed class UserApi : WorkerEntrypoint
{
    public async Task<User?> GetUserAsync(string id) => await Env.D1("DB")
        .Prepare("SELECT id, username, created_at AS createdAt FROM users WHERE id = ?").Bind(id).FirstAsync<User>();

    public async Task<IReadOnlyList<User>> SearchUsersAsync(string prefix, int limit = 10)
    {
        var safeLimit = (int)Numbers.Min(Numbers.Max(limit, 1), 50);
        var result = await Env.D1("DB")
            .Prepare("SELECT id, username, created_at AS createdAt FROM users WHERE username LIKE ? ORDER BY username LIMIT ?")
            .Bind($"{prefix}%", safeLimit).AllAsync<User>();
        return result.Results;
    }

    public Task<Response> GetAssetAsync(string path)
    {
        if (!path.StartsWith("/"))
            throw new ArgumentException("Asset path must begin with /");
        return Env.Assets("ASSETS").FetchAsync(new Request($"https://assets.local{path}"));
    }
}

public sealed class AdminApi : WorkerEntrypoint
{
    public async Task<CreateUserResult> CreateUserAsync(CreateUserInput input)
    {
        if (!ValidateUsername(input.Username))
            return new(null, "Invalid username");
        var user = new User(Crypto.RandomUuid(), input.Username, new Date().ToIsoString());
        await Env.D1("DB").Prepare("INSERT INTO users (id, username, created_at) VALUES (?, ?, ?)")
            .Bind(user.Id, user.Username, user.CreatedAt).RunAsync();
        Ctx.WaitUntil(Task.CompletedTask);
        return new(user, null);
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var result = await Env.D1("DB").Prepare("DELETE FROM users WHERE id = ?").Bind(id).RunAsync();
        return result.Meta.Changes > 0;
    }

    private bool ValidateUsername(string value) =>
        value.Length >= 3 && value.Length <= 32 && new RegExp("^[a-zA-Z0-9_-]+$").Test(value);
}

public sealed class CoreService : WorkerEntrypoint
{
    private int _iteratorOffset;

    public async Task<string> AsyncControlFlowAsync(int value)
    {
        var url = value < 0 ? null : new Url("https://worker.test/?value=present");
        return value switch
        {
            0 => await Task.FromResult("awaited"),
            _ => url?.SearchParams.Get(await Task.FromResult("value")) ?? "missing"
        };
    }

    public async Task<int[]> IteratorSnapshotAsync(int start)
    {
        _iteratorOffset = 10;
        var sequence = ValuesAsync(start);
        var values = new List<int>();
        await foreach (var value in sequence) values.Add(value);
        _iteratorOffset = 20;
        await foreach (var value in sequence) values.Add(value);
        return values.ToArray();
    }

    private async IAsyncEnumerable<int> ValuesAsync(int start)
    {
        await Task.CompletedTask;
        yield return _iteratorOffset + start++;
        yield return _iteratorOffset + start++;
    }

    public Health Health() => new(true, "core", new Date().ToIsoString());
    public override Task<Response> FetchAsync(Request request) => Task.FromResult(new Url(request.Url).Pathname == "/internal/health"
        ? Response.Json(Health())
        : new Response("Not found") { Status = 404 });
}

public sealed record CreateUserInput(string Username);
public sealed record CreateUserResult(User? User, string? Error);
public sealed record User(string Id, string Username, string CreatedAt);
public sealed record Health(bool Ok, string Service, string Timestamp);
