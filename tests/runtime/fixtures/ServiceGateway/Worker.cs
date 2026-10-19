using Workers;

namespace ServiceGateway;

public static class Gateway
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        var url = new Url(request.Url);
        var path = url.Pathname;
        if (path == "/rpc-key-collections")
        {
            var core = environment.Service("CORE");
            var numbers = new Dictionary<int, int> { [1] = 2, [3] = 4 };
            var keys = new HashSet<RpcKey> { new RpcKey(7) };
            var map = await core.InvokeAsync<Dictionary<int, RpcUser>>("buildNumberMap");
            return Response.Json(new {
                sum = await core.InvokeAsync<int>("sumNumberMap", numbers),
                contains = await core.InvokeAsync<bool>("hasCompositeSet", keys),
                description = map![1].Describe()
            });
        }
        if (path == "/rpc-typed-graph")
        {
            var graph = await environment.Service("CORE").InvokeAsync<RpcGraph>("loadGraph");
            return new Response(graph!.Describe());
        }
        if (path == "/typed-service")
        {
            var user = await environment.Get<CoreService>("CORE").LoadUserAsync();
            var graph = await environment.Get<CoreService>("CORE").LoadGraphAsync();
            var echoed = await environment.Get<CoreService>("CORE").EchoUserAsync(user);
            return Response.Json(new { name = user.Name, description = echoed.Describe(), graph = graph.Describe() });
        }
        if (path == "/rpc-graphs")
        {
            var graph = new RpcGraph();
            graph.Next = graph;
            object?[] array = new object?[1];
            array[0] = array;
            var core = environment.Service("CORE");
            return Response.Json(new {
                cycle = await core.InvokeAsync<bool>("hasCycle", graph),
                arrayCycle = await core.InvokeAsync<bool>("hasArrayCycle", (object)array),
                shared = await core.InvokeAsync<bool>("sameReference", graph, graph)
            });
        }
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

public interface ICoreView
{
    RpcUser BuildUser();
    T Echo<T>(T value) => value;
    string Describe() => BuildUser().Describe();
    string Title => BuildUser().Describe();
}

public interface ICovariantCore<out T>
{
    T BuildUser();
}

public sealed class LocalCore : ICoreView
{
    public RpcUser BuildUser() => new RpcUser("Local");
}

public sealed class CoreService : WorkerEntrypoint, ICoreView, ICovariantCore<RpcUser>
{
    private int _constructorValue;
    private (int, int) _initializerPair = RpcValueSource.Pair;
    private (int, int) _returnPair = (3, 4);
    private int _iteratorOffset;
    private RpcUser _sharedUser = new RpcUser("Ada");
    private int _touches;

    public CoreService() => _constructorValue = RpcValueSource.TryInitial(out var value) ? value : 0;
    public int ConstructorValue() => _constructorValue;

    public bool FieldInitializerSnapshot()
    {
        var original = _initializerPair.Item1;
        RpcValueSource.Pair.Item1 = original + 1;
        return _initializerPair.Item1 == original;
    }
    private async Task<(int, int)> ReadPairAsync() =>
        _returnPair.Item1 == 3 ? _returnPair : await Task.FromResult(_returnPair);
    public async Task<int> AsyncReturnSnapshot()
    {
        var pending = ReadPairAsync();
        _returnPair.Item1 = 9;
        var snapshot = await pending;
        return snapshot.Item1;
    }

    public RpcUser BuildUser() => _sharedUser;
    public bool HasTypedAliases(RpcBase first, RpcDerived second) =>
        first == second && second.Describe() == "base:user:Ada";
    public bool HasReversedTypedAliases(RpcDerived first, RpcBase second) =>
        first == second && first.Describe() == "base:user:Ada";
    public bool HasTypedArrayAliases(RpcBase[] first, RpcDerived[] second) =>
        first == second && second[0].Describe() == "base:user:Ada";
    public bool HasReversedTypedArrayAliases(RpcDerived[] first, RpcBase[] second) =>
        first == second && first[0].Describe() == "base:user:Ada";
    public T Echo<T>(T value) { Touch(); return value; }
    private void Touch() => Increment();
    private void Increment() => _touches++;
    public int UseGenericInterface()
    {
        ICoreView service = this;
        ICoreView local = new LocalCore();
        Func<int, int> echo = service.Echo<int>;
        Func<int, int> repeated = service.Echo<int>;
        Func<int, int> localEcho = local.Echo<int>;
        if (echo != repeated || echo(3) != 3 || localEcho(4) != 4) return -1;
        var result = service.Echo(7) + local.Echo(8);
        return _touches == 2 ? result : -2;
    }
    public string UseUserThroughInterface()
    {
        ICoreView service = this;
        return service.BuildUser().Describe();
    }
    public bool ImplementsCoreInterface() => this is ICoreView;
    public string UseDefaultInterface()
    {
        ICoreView service = this;
        return service.Describe();
    }
    public string UseDefaultProperty() => ((ICoreView)this).Title;
    public string UseInterfaceDelegate()
    {
        ICoreView service = this;
        Func<RpcUser> factory = service.BuildUser;
        Func<RpcUser> repeated = service.BuildUser;
        ICovariantCore<RpcUser> covariant = this;
        Func<RpcUser> sameMethod = covariant.BuildUser;
        if (factory != repeated || factory != sameMethod) return "delegate identity mismatch";
        ICoreView local = new LocalCore();
        Func<RpcUser> localFactory = local.BuildUser;
        if (localFactory().Describe() != "user:Local") return "ordinary receiver mismatch";
        return factory().Describe();
    }
    public string UseMixedInterfaces() => RpcCaller.DescribeInterface(this) + ":" + RpcCaller.DescribeInterface(new LocalCore());
    public bool UseCovariantInterface()
    {
        object service = this;
        return service is ICovariantCore<object> && ((ICovariantCore<object>)service).BuildUser() == _sharedUser;
    }
    public int SumNumberMap(Dictionary<int, int> numbers) => numbers[1] + numbers[3];
    public bool NumberMapKeepsCycle(Dictionary<int, object> values) => values[1] == values;
    public bool HasCompositeSet(HashSet<RpcKey> keys) => keys.Contains(new RpcKey(7));
    public Dictionary<int, RpcUser> BuildNumberMap() => new() { [1] = _sharedUser };
    public Dictionary<int, int> BuildNumberTotals() => new() { [1] = 2, [3] = 4 };
    public HashSet<(int, int)> BuildTupleSet() => new() { (7, 8) };
    public Task<RpcUser> LoadUserAsync() => Task.FromResult(_sharedUser);
    public Task<RpcUser> EchoUserAsync(RpcUser user) => Task.FromResult(user);
    public Task<RpcGraph> LoadGraphAsync()
    {
        var graph = new RpcGraph();
        graph.Next = graph;
        return Task.FromResult(graph);
    }
    public string UsePublicUser() => BuildUser().Describe();
    public async Task<string> UseAwaitedUserAsync() => (await LoadUserAsync()).Describe();
    public bool PublicUserKeepsIdentity() => BuildUser() == _sharedUser;
    public bool ExplicitUserKeepsIdentity() => this.BuildUser() == _sharedUser;
    public string UseUserFromHelper() => RpcCaller.Describe(this);
    public object BuildBoxedUser() => _sharedUser;
    public IRpcUser BuildInterfaceUser() => _sharedUser;
    public object[] BuildBoxedUsers() => new object[] { _sharedUser, _sharedUser };
    public RpcUser BuildDefaultUser(string name = "Grace") => new RpcUser(name);
    public string UseDefaultUser() => BuildDefaultUser().Describe();
    public string DescribeIncoming(RpcUser user) => user.Describe();
    public string DescribeBuiltUser() => DescribeIncoming(BuildUser());
    public bool IncomingUserKeepsIdentity(RpcUser first, RpcUser second) => first == second;
    public string DescribeIncomingGraph(RpcGraph graph) => graph.Next!.Describe();
    public string DescribeIncomingUsers(RpcEnvelope envelope) => envelope.Users[0].Describe();
    public string DescribeQueue(Queue<RpcUser> users) => users.Peek().Describe();
    public string DescribeStack(Stack<RpcUser> users) => users.Peek().Describe();
    public string DescribeSetInterface(ISet<RpcUser> users) => users.First().Describe();
    public string DescribeReadOnlySet(IReadOnlySet<RpcUser> users) => users.First().Describe();
    public string DescribeUserPairs(IEnumerable<KeyValuePair<string, RpcUser>> pairs) => pairs.First().Value.Describe();
    public object CollectionSummary(IReadOnlyCollection<int> values)
    {
        int total = 0, visits = 0;
        foreach (var value in values)
        {
            total += value;
            if (++visits == 4) break;
        }
        return new { count = values.Count, total, visits };
    }
    public string DescribeCtorProbe(RpcCtorProbe probe) => probe.Inspect();
    public RpcPrivateState BuildPrivateState() => new RpcPrivateState(_sharedUser);
    public string DescribePrivateState(RpcPrivateState state) => state.Describe();
    public RpcPrimaryState BuildPrimaryState() => new RpcPrimaryState(_sharedUser);
    public string DescribePrimaryState(RpcPrimaryState state) => state.Describe();
    public bool DescribeCollectionData(RpcCollectionData data) =>
        data.Array[0].Describe() == "user:Ada" && data.ByName["x"] == data.Array[0]
        && data.Pair.Item1 == data.Array[0] && data.Pair.Item2 == data.Array[0]
        && data.Sequence.First() == data.Array[0] && data.Points.Contains(3);

    public bool HasCycle(JsonValue value) => value["next"] == value;
    public bool HasArrayCycle(JsonValue value) => value[0] == value;
    public bool SameReference(JsonValue left, JsonValue right) => left == right;

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
public sealed record RpcKey(int Id);

public sealed class RpcGraph
{
    public RpcGraph? Next { get; set; }
    public string Describe() => Next == this ? "self" : "other";
}

public class RpcBase
{
    public string Name { get; set; } = "base";
}

public sealed class RpcDerived : RpcBase
{
    public RpcUser User { get; set; } = new RpcUser("Ada");
    public string Describe() => Name + ":" + User.Describe();
}

public interface IRpcUser
{
    string Name { get; }
    string Describe();
}

public sealed class RpcUser : IRpcUser
{
    public RpcUser(string name) { Name = name; }
    public string Name { get; }
    public string Describe() => "user:" + Name;
}

public static class RpcCaller
{
    public static string Describe(CoreService service) => service.BuildUser().Describe();
    public static string DescribeInterface(ICoreView service) => service.BuildUser().Describe();
}

public sealed class RpcEnvelope
{
    public RpcEnvelope(List<RpcUser> users) { Users = users; }
    public List<RpcUser> Users { get; }
}

public sealed class RpcCtorProbe
{
    private static int calls;
    public RpcCtorProbe() { calls++; }
    public string Inspect() => "calls:" + calls;
}

public sealed class RpcPrivateState
{
    private RpcUser user;
    public RpcPrivateState(RpcUser user) { this.user = user; }
    public string Describe() => user.Describe();
}

public sealed class RpcPrimaryState(RpcUser user)
{
    public string Describe() => user.Describe();
}

public sealed class RpcCollectionData
{
    public RpcUser[] Array { get; init; } = [];
    public Dictionary<string, RpcUser> ByName { get; init; } = new();
    public (RpcUser, RpcUser) Pair { get; init; }
    public IEnumerable<RpcUser> Sequence { get; init; } = [];
    public HashSet<int> Points { get; init; } = [];
}

public static class RpcValueSource
{
    public static (int, int) Pair = (1, 2);
    public static bool TryInitial(out int value) { value = 7; return true; }
}
