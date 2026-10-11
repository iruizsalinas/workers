using static BindingIntrinsicRegistry;

internal static class KvIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.KVNamespace", "GetAsync")] = new("text", BindingIntrinsicKind.KvGet),
            [Key("Workers.KVNamespace", "GetJsonAsync")] = new("json", BindingIntrinsicKind.KvGet),
            [Key("Workers.KVNamespace", "GetBytesAsync")] = new("arrayBuffer", BindingIntrinsicKind.KvGet),
            [Key("Workers.KVNamespace", "GetStreamAsync")] = new("stream", BindingIntrinsicKind.KvGet),
            [Key("Workers.KVNamespace", "GetWithMetadataAsync")] = new("text", BindingIntrinsicKind.KvGetWithMetadata),
            [Key("Workers.KVNamespace", "GetJsonWithMetadataAsync")] = new("json", BindingIntrinsicKind.KvGetWithMetadata),
            [Key("Workers.KVNamespace", "GetBytesWithMetadataAsync")] = new("arrayBuffer", BindingIntrinsicKind.KvGetWithMetadata),
            [Key("Workers.KVNamespace", "GetStreamWithMetadataAsync")] = new("stream", BindingIntrinsicKind.KvGetWithMetadata),
            [Key("Workers.KVNamespace", "PutAsync")] = Direct("put"),
            [Key("Workers.KVNamespace", "PutJsonAsync")] = new("put", BindingIntrinsicKind.KvJsonPut),
            [Key("Workers.KVNamespace", "DeleteAsync")] = Direct("delete"),
            [Key("Workers.KVNamespace", "ListAsync")] = Direct("list")
        };
}
