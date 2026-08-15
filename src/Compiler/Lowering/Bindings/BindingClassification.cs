using static BindingIntrinsicRegistry;

internal static class BindingClassification
{
    private static readonly HashSet<string> StructuralTypes =
    [
        "Workers.KvGetOptions", "Workers.KvPutOptions", "Workers.KvListOptions",
        "Workers.R2GetOptions", "Workers.R2PutOptions", "Workers.R2ListOptions",
        "Workers.R2MultipartUploadOptions", "Workers.R2Conditional", "Workers.R2Range",
        "Workers.R2HttpMetadata", "Workers.R2Checksums", "Workers.QueueSendOptions",
        "Workers.QueueRetryOptions", "Workers.CacheQueryOptions", "Workers.FetchOptions",
        "Workers.FetchCfOptions", "Workers.D1RawOptions",
        "Workers.DurableObjectIdOptions", "Workers.DurableObjectGetOptions", "Workers.WebSocketAutoResponse",
        "Workers.AnalyticsEngineDataPoint", "Workers.SendEmailMessage", "Workers.WorkflowInstanceCreateOptions",
        "Workers.WorkflowInstanceRestartOptions", "Workers.WorkflowInstanceEventOptions", "Workers.WorkflowRestartFromStep",
        "Workers.ImagesOutputOptions", "Workers.MediaOutputOptions", "Workers.VectorizeVector",
        "Workers.VectorizeQueryOptions", "Workers.DurableObjectStorageReadOptions", "Workers.DurableObjectStorageWriteOptions",
        "Workers.DurableObjectStorageListOptions", "Workers.DurableObjectKvListOptions", "Workers.ContainerStartOptions",
        "Workers.ContainerExecOptions", "Workers.TcpSocketAddress", "Workers.TcpSocketOptions"
    ];

    private static readonly HashSet<(string Type, string Method)> SpecialMethods =
    [
        Key("Workers.Env", "Get"), Key("Workers.Env", "Variable"),
        Key("Workers.Env", "Secret"), Key("Workers.Env", "Raw"), Key("Workers.Env", "Kv"),
        Key("Workers.Env", "R2"), Key("Workers.Env", "Service"), Key("Workers.Env", "Assets"),
        Key("Workers.Env", "Mtls"), Key("Workers.Env", "Dispatcher"), Key("Workers.Env", "Queue"),
        Key("Workers.Env", "D1"), Key("Workers.Env", "DurableObject"),
        Key("Workers.Env", "RateLimiter"), Key("Workers.Env", "Analytics"), Key("Workers.Env", "Email"),
        Key("Workers.Env", "Version"), Key("Workers.Env", "Ai"), Key("Workers.Env", "Workflow"),
        Key("Workers.Env", "Images"), Key("Workers.Env", "Media"), Key("Workers.Env", "Vectorize"),
        Key("Workers.Env", "SecretStore"), Key("Workers.Env", "Hyperdrive"),
        Key("Workers.CacheStorage", "OpenAsync"), Key("Workers.Http", "FetchAsync"),
        Key("Workers.TcpSocket", "Connect"), Key("Workers.WebSocketPair", "Create"),
        Key("Workers.Body", "Text"), Key("Workers.Body", "Json"), Key("Workers.Body", "FromBytes"),
        Key("Workers.Response", "Empty"), Key("Workers.Response", "Text"), Key("Workers.Response", "Html"),
        Key("Workers.Response", "Json"), Key("Workers.Response", "Redirect"),
        Key("Workers.Response", "FromBody"), Key("Workers.Response", "WithHeader"), Key("Workers.Response", "AppendHeader"),
        Key("Workers.Response", "FromStream"),
        Key("Workers.Response", "WebSocket"),
        Key("Workers.Response", "WithoutHeader")
    ];

    private static readonly HashSet<(string Type, string Method)> UnsupportedMethods =
    [
        Key("Workers.Env", "TryGet"), Key("Workers.QueryParameters", "As"),
        Key("Workers.HtmlElement", "OnEndTag"),
        Key("Workers.QueueMessageBatch<T>", "GetEnumerator"), Key("Workers.TailEvent", "GetEnumerator"),
        Key("Workers.Headers", "GetEnumerator"), Key("Workers.QueryParameters", "GetEnumerator")
    ];

    private static readonly HashSet<string> UnsupportedTypes =
    [
        "Workers.HtmlElementHandler", "Workers.HtmlDocumentHandler", "Workers.QueueSendRequest", "Workers.RpcClient"
    ];

    public static bool IsStructural(string type) => StructuralTypes.Contains(type);

    public static bool IsClassified((string Type, string Method) key, bool hasIntrinsic) =>
        hasIntrinsic || SpecialMethods.Contains(key) || UnsupportedMethods.Contains(key) || UnsupportedTypes.Contains(key.Type);
}
