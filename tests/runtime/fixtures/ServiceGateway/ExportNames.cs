using Workers;

namespace ExportNameCollisions;

public sealed class Object : WorkerEntrypoint
{
    private int Math = 2;
    private int Count = 3;
    private int @default = 4;
    public string Ping() => $"{Math * Count}:{@default}";
}

public sealed class ReservedFields : WorkerEntrypoint
{
    private int env = 1;
    private int ctx = 2;
    private int __proto__ = 3;
    private int ping = 4;
    public int Ping() => env + ctx + __proto__ + ping;
    public Task<string?> CoreAsync() => Env.Service("CORE").InvokeAsync<string>("usePublicUser");
}
