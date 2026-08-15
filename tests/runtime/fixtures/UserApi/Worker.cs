using Workers;

namespace UserApi;

public static class Worker
{
    private const ulong CacheTtlSeconds = 60;

    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        try
        {
            var parts = request.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (request.Method == "POST" && parts.Length == 1 && parts[0] == "users")
                return await CreateUserAsync(request, environment, context);

            if (parts.Length == 2 && parts[0] == "users")
            {
                var id = parts[1];
                switch (request.Method)
                {
                    case "GET":
                        var user = await GetUserAsync(id, environment, context);
                        return user is null
                            ? Error("User not found", 404)
                            : Response.Json(user);
                    case "DELETE":
                        return await DeleteUserAsync(id, environment);
                    default:
                        return Error("Method not allowed", 405).WithHeader("allow", "GET, DELETE");
                }
            }

            return Error("Not found", 404);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Error("Internal server error", 500);
        }
    }

    private static async Task<UserView?> GetUserAsync(string id, Env environment, Context context)
    {
        var key = CacheKey(id);
        var users = environment.Kv("USERS");
        var cached = await users.GetJsonAsync<User>(key);
        if (cached is not null)
            return new UserView(cached.Id, cached.Name, cached.Email, cached.CreatedAt, "kv");

        var user = await environment.D1("DB")
            .Prepare("SELECT id, name, email, created_at AS createdAt FROM users WHERE id = ?")
            .Bind(id)
            .FirstAsync<User>();
        if (user is null)
            return null;

        context.WaitUntil(users.PutJsonAsync(key, user, new KvPutOptions { ExpirationTtl = CacheTtlSeconds }));
        return new UserView(user.Id, user.Name, user.Email, user.CreatedAt, "d1");
    }

    private static async Task<Response> CreateUserAsync(Request request, Env environment, Context context)
    {
        var input = await request.JsonAsync<CreateUserInput>();
        if (input is null || input.Name.Trim().Length < 2 || !input.Email.Contains('@'))
            return Error("Invalid name or email", 400);

        var user = new User(
            Guid.NewGuid().ToString(),
            input.Name.Trim(),
            input.Email.Trim().ToLowerInvariant(),
            DateTimeOffset.UtcNow.ToString("O"));

        await environment.D1("DB")
            .Prepare("INSERT INTO users (id, name, email, created_at) VALUES (?, ?, ?, ?)")
            .Bind(user.Id, user.Name, user.Email, user.CreatedAt)
            .RunAsync();

        context.WaitUntil(environment.Kv("USERS").PutJsonAsync(
            CacheKey(user.Id), user, new KvPutOptions { ExpirationTtl = CacheTtlSeconds }));

        return Response.Json(user, 201).WithHeader("location", $"/users/{user.Id}");
    }

    private static async Task<Response> DeleteUserAsync(string id, Env environment)
    {
        var database = environment.D1("DB");
        var existing = await database.Prepare("SELECT id FROM users WHERE id = ?").Bind(id).FirstAsync<UserId>();
        if (existing is null)
            return Error("User not found", 404);

        await database.Prepare("DELETE FROM users WHERE id = ?").Bind(id).RunAsync();
        await environment.Kv("USERS").DeleteAsync(CacheKey(id));
        return Response.Empty(204);
    }

    private static string CacheKey(string id) => $"user:{id}";
    private static Response Error(string message, int status) => Response.Json(new { error = message }, status);
}

public sealed record CreateUserInput(string Name, string Email);
public sealed record User(string Id, string Name, string Email, string CreatedAt);
public sealed record UserId(string Id);
public sealed record UserView(string Id, string Name, string Email, string CreatedAt, string Source);
