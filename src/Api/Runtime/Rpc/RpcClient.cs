namespace Workers;

public static class RpcClient
{
    public static T AsRpc<T>(this IServiceBinding binding) => WorkerApi.NotExecutable<T>();
    public static T AsRpc<T>(this IDurableObjectStub stub) => WorkerApi.NotExecutable<T>();
    public static T AsRpc<T>(this RpcStub stub) => WorkerApi.NotExecutable<T>();
    public static T GetRpc<T>(this IDynamicDispatcherBinding dispatcher, string name) => WorkerApi.NotExecutable<T>();
}
