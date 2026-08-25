using static BindingIntrinsicRegistry;

internal static class
KvIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.IKvNamespace", "GetTextAsync")] = Direct("get"),
            [Key("Workers.IKvNamespace", "GetTextWithMetadataAsync")] = Direct("getWithMetadata"),
            [Key("Workers.IKvNamespace", "GetTextBulkAsync")] = new("get", BindingIntrinsicKind.DictionaryObject),
            [Key("Workers.IKvNamespace", "GetTextBulkWithMetadataAsync")] = Direct("getWithMetadata"),
            [Key("Workers.IKvNamespace", "GetBytesAsync")] = new("get", BindingIntrinsicKind.KvBytesGet),
            [Key("Workers.IKvNamespace", "GetBytesWithMetadataAsync")] = new("getWithMetadata", BindingIntrinsicKind.KvBytesGet),
            [Key("Workers.IKvNamespace", "GetJsonAsync")] = new("get", BindingIntrinsicKind.KvJsonGet),
            [Key("Workers.IKvNamespace", "GetJsonWithMetadataAsync")] = new("getWithMetadata", BindingIntrinsicKind.KvJsonGet),
            [Key("Workers.IKvNamespace", "GetJsonBulkAsync")] = new("get", BindingIntrinsicKind.KvJsonGet),
            [Key("Workers.IKvNamespace", "GetJsonBulkWithMetadataAsync")] = new("getWithMetadata", BindingIntrinsicKind.KvJsonGet),
            [Key("Workers.IKvNamespace", "PutTextAsync")] = Direct("put"),
            [Key("Workers.IKvNamespace", "PutBytesAsync")] = Direct("put"),
            [Key("Workers.IKvNamespace", "PutJsonAsync")] = new("put", BindingIntrinsicKind.KvJsonPut),
            [Key("Workers.IKvNamespace", "DeleteAsync")] = Direct("delete"),
            [Key("Workers.IKvNamespace", "ListAsync")] = Direct("list"),

        };
}
