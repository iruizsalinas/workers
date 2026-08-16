using System.Text.RegularExpressions;
using Workers;

namespace ServiceGateway;

public static class Gateway
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Method == "GET" && request.Path == "/health")
        {
            var rpc = await environment.Service("CORE").InvokeAsync<Health>("health");
            var http = await environment.Service("CORE").FetchAsync("https://service/internal/health");
            return Response.Json(new { rpc, http = await http.JsonAsync<Health>() });
        }
        if (request.Method == "POST" && request.Path == "/users")
        {
            try
            {
                var input = await request.JsonAsync<CreateUserInput>();
                var user = await environment.Service("ADMIN").InvokeAsync<User>("createUser", [input]);
                return Response.Json(user, 201);
            }
            catch (Exception exception)
            {
                return Response.Json(new { error = exception.Message }, 400);
            }
        }
        if (request.Method == "GET" && request.Path == "/users")
        {
            var prefix = request.QueryParameters.Get("prefix") ?? "";
            var users = await environment.Service("USERS").InvokeAsync<IReadOnlyList<User>>("searchUsers", [prefix]);
            return Response.Json(new { users });
        }
        if (request.Method == "GET" && request.Path.StartsWith("/users/"))
        {
            var id = request.Path.Substring(7);
            var user = await environment.Service("USERS").InvokeAsync<User>("getUser", [id]);
            return user is null ? Response.Json(new { error = "Not found" }, 404) : Response.Json(user);
        }
        if (request.Method == "GET" && request.Path.StartsWith("/assets/"))
        {
            var asset = await environment.Service("USERS").InvokeAsync<Response>("getAsset", [request.Path.Substring(7)]);
            return asset is null ? Response.Text("Missing asset", 502) : asset.WithHeader("x-served-via", "rpc");
        }
        return Response.Json(new { error = "Not found" }, 404);
    }
}

[WorkerEntrypoint("UserApi")]
public sealed class UserApi : WorkerEntrypoint
{
    public async Task<User?> GetUserAsync(string id) => await Environment.D1("DB")
        .Prepare("SELECT id, username, created_at AS createdAt FROM users WHERE id = ?").Bind(id).FirstAsync<User>();

    public async Task<IReadOnlyList<User>> SearchUsersAsync(string prefix, int limit = 10)
    {
        var safeLimit = Math.Min(Math.Max(limit, 1), 50);
        var result = await Environment.D1("DB")
            .Prepare("SELECT id, username, created_at AS createdAt FROM users WHERE username LIKE ? ORDER BY username LIMIT ?")
            .Bind($"{prefix}%", safeLimit).AllAsync<User>();
        return result.Results;
    }

    public Task<Response> GetAssetAsync(string path)
    {
        if (!path.StartsWith("/"))
            throw new ArgumentException("Asset path must begin with /");
        return Environment.Assets("ASSETS").FetchAsync(new Request($"https://assets.local{path}"));
    }
}

[WorkerEntrypoint("AdminApi")]
public sealed class AdminApi : WorkerEntrypoint
{
    public async Task<User> CreateUserAsync(CreateUserInput input)
    {
        if (!ValidateUsername(input.Username))
            throw new ArgumentException("Invalid username");
        var user = new User(Guid.NewGuid().ToString(), input.Username, DateTimeOffset.UtcNow.ToString("O"));
        await Environment.D1("DB").Prepare("INSERT INTO users (id, username, created_at) VALUES (?, ?, ?)")
            .Bind(user.Id, user.Username, user.CreatedAt).RunAsync();
        Context.WaitUntil(Task.CompletedTask);
        return user;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var result = await Environment.D1("DB").Prepare("DELETE FROM users WHERE id = ?").Bind(id).RunAsync();
        return result.Meta.Changes > 0;
    }

    private static bool ValidateUsername(string value) =>
        value.Length >= 3 && value.Length <= 32 && Regex.IsMatch(value, "^[a-zA-Z0-9_-]+$");
}

[WorkerEntrypoint("CoreService")]
public sealed class CoreService : WorkerEntrypoint
{
    public Health Health() => new(true, "core", DateTimeOffset.UtcNow.ToString("O"));
    public Response FetchAsync(Request request) => request.Path == "/internal/health"
        ? Response.Json(Health())
        : Response.Text("Not found", 404);
}

public sealed record CreateUserInput(string Username);
public sealed record User(string Id, string Username, string CreatedAt);
public sealed record Health(bool Ok, string Service, string Timestamp);
