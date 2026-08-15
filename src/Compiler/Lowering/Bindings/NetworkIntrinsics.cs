using static BindingIntrinsicRegistry;

internal static class
NetworkIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.ICache", "PutAsync")] = Direct("put"),
            [Key("Workers.ICache", "MatchAsync")] = new("match", BindingIntrinsicKind.CacheMatch),
            [Key("Workers.ICache", "DeleteAsync")] = new("delete", BindingIntrinsicKind.CacheQuery),

            // Service/fetcher bindings
            [Key("Workers.IFetcherBinding", "FetchAsync")] = Direct("fetch"),
            [Key("Workers.IServiceBinding", "InvokeAsync")] = new("", BindingIntrinsicKind.ServiceRpc),
            [Key("Workers.IServiceBinding", "InvokeStubAsync")] = new("", BindingIntrinsicKind.ServiceRpc),
            [Key("Workers.IServiceBinding", "InvokeVoidAsync")] = new("", BindingIntrinsicKind.ServiceRpc),

        };
}
