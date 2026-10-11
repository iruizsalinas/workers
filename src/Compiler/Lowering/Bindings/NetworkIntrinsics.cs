using static BindingIntrinsicRegistry;

internal static class NetworkIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.Cache", "PutAsync")] = Direct("put"),
            [Key("Workers.Cache", "MatchAsync")] = new("match", BindingIntrinsicKind.CacheMatch),
            [Key("Workers.Cache", "DeleteAsync")] = Direct("delete"),
            [Key("Workers.Fetcher", "FetchAsync")] = Direct("fetch"),
            [Key("Workers.Fetcher", "Connect")] = Direct("connect"),
            [Key("Workers.Fetcher", "InvokeAsync")] = new("", BindingIntrinsicKind.Rpc),
            [Key("Workers.Fetcher", "InvokeStubAsync")] = new("", BindingIntrinsicKind.Rpc),
            [Key("Workers.DispatchNamespace", "Get")] = Direct("get"),
            [Key("Workers.RpcStub", "InvokeAsync")] = new("", BindingIntrinsicKind.Rpc),
            [Key("Workers.RpcStub", "InvokeStubAsync")] = new("", BindingIntrinsicKind.Rpc),
            [Key("Workers.RpcStub", "Dup")] = Direct("dup"),
            [Key("Workers.RpcStub", "Dispose")] = new("", BindingIntrinsicKind.Dispose),
            [Key("Workers.RawBinding", "GetProperty")] = new("", BindingIntrinsicKind.PropertyByName),
            [Key("Workers.RawBinding", "InvokeAsync")] = new("", BindingIntrinsicKind.Rpc),
            [Key("Workers.Socket", "CloseAsync")] = Direct("close"),
            [Key("Workers.Socket", "StartTls")] = Direct("startTls")
        };
}
