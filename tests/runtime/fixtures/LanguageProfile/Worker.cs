using Workers;

namespace LanguageProfile;

public sealed record Page<T>(List<T> Items);
public sealed record User(string Name);
public abstract record Result<T>(string Kind);
public sealed record Success<T>(T Value) : Result<T>("success");
public sealed record Failure<T>(string Message) : Result<T>("failure");

public interface IHandler<T>
{
    Result<T> Handle(T value);
}

public abstract class Handler<T> : IHandler<T>
{
    public abstract Result<T> Handle(T value);
}

public sealed class UserHandler : Handler<User>
{
    private string prefix;
    public UserHandler() : this("Hello") { }
    public UserHandler(string prefix) { this.prefix = prefix; }
    public override Result<User> Handle(User user) => TryName(user, out var name)
        ? new Success<User>(new User($"{prefix}, {name}"))
        : new Failure<User>("Name is empty");
    private static bool TryName(User user, out string name)
    {
        name = user.Name.Trim();
        return name.Length != 0;
    }
}

public static class ResultExtensions
{
    public static T Unwrap<T>(this Result<T> result) => result switch
    {
        Success<T> success => success.Value,
        _ => throw new Exception("Expected success")
    };
}

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request)
    {
        if (new Url(request.Url).Pathname == "/semantics") return Semantics.Run();
        var page = await request.JsonAsync<Page<User>>();
        IHandler<User> handler = new UserHandler();
        var result = handler.Handle(page!.Items[0]);
        return Response.Json(new { kind = result.Kind, user = result.Unwrap() });
    }
}
