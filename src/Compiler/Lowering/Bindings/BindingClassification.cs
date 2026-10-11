using static BindingIntrinsicRegistry;

internal static class BindingClassification
{
    private static readonly HashSet<string> StructuralTypes =
    [
        "Workers.RequestInit", "Workers.RequestInitCfProperties", "Workers.CacheQueryOptions", "Workers.CookieOptions",
        "Workers.KVNamespaceGetOptions", "Workers.KVNamespacePutOptions", "Workers.KVNamespaceListOptions",
        "Workers.R2GetOptions", "Workers.R2PutOptions", "Workers.R2ListOptions", "Workers.R2MultipartOptions",
        "Workers.R2Conditional", "Workers.R2Range", "Workers.R2HttpMetadata", "Workers.R2UploadedPart",
        "Workers.D1RawOptions", "Workers.QueueSendOptions", "Workers.QueueSendBatchOptions", "Workers.MessageSendRequest<T>",
        "Workers.QueueRetryOptions",
        "Workers.DurableObjectNamespaceNewUniqueIdOptions", "Workers.DurableObjectNamespaceGetDurableObjectOptions",
        "Workers.DurableObjectGetOptions", "Workers.DurableObjectPutOptions", "Workers.DurableObjectListOptions",
        "Workers.SyncKvListOptions", "Workers.ContainerStartupOptions", "Workers.ContainerExecOptions",
        "Workers.RateLimitOptions", "Workers.AnalyticsEngineDataPoint", "Workers.EmailMessageBuilder",
        "Workers.WorkflowInstanceCreateOptions", "Workers.WorkflowInstanceTerminateOptions", "Workers.WorkflowInstanceRestartOptions",
        "Workers.WorkflowRestartStep", "Workers.WorkflowInstanceEvent", "Workers.ImageOutputOptions",
        "Workers.MediaTransformationOutputOptions", "Workers.VectorizeVector", "Workers.VectorizeQueryOptions",
        "Workers.SocketAddress", "Workers.SocketOptions", "Workers.StreamPipeOptions", "Workers.HtmlContentOptions",
        "Workers.AesGcmParams", "Workers.AesCbcParams", "Workers.AesCtrParams", "Workers.AesKwParams",
        "Workers.RsaOaepParams", "Workers.HmacParams", "Workers.RsaPkcs1Params", "Workers.RsaPssParams",
        "Workers.EcdsaParams", "Workers.Ed25519Params", "Workers.NodeEd25519Params", "Workers.X25519Params",
        "Workers.MlDsaParams", "Workers.MlKemParams", "Workers.AesKeyParams", "Workers.HmacKeyParams",
        "Workers.RsaKeyGenParams", "Workers.RsaKeyImportParams", "Workers.EcKeyParams", "Workers.Pbkdf2KeyParams",
        "Workers.HkdfKeyParams", "Workers.Pbkdf2Params", "Workers.HkdfParams", "Workers.EcdhKeyDeriveParams",
        "Workers.JsonWebKey", "Workers.RsaOtherPrimesInfo", "Workers.DigestStreamOptions"
    ];

    private static readonly HashSet<(string Type, string Method)> SpecialMethods =
    [
        Key("Workers.Env", "Get"), Key("Workers.Env", "TryGet"), Key("Workers.Env", "Variable"),
        Key("Workers.Env", "Secret"), Key("Workers.Env", "Raw"), Key("Workers.Env", "KV"),
        Key("Workers.Env", "R2"), Key("Workers.Env", "D1"), Key("Workers.Env", "Queue"),
        Key("Workers.Env", "DurableObject"), Key("Workers.Env", "Service"), Key("Workers.Env", "Assets"),
        Key("Workers.Env", "MtlsCertificate"), Key("Workers.Env", "DispatchNamespace"), Key("Workers.Env", "RateLimit"),
        Key("Workers.Env", "AnalyticsEngine"), Key("Workers.Env", "SendEmail"), Key("Workers.Env", "VersionMetadata"),
        Key("Workers.Env", "Ai"), Key("Workers.Env", "Workflow"), Key("Workers.Env", "Images"),
        Key("Workers.Env", "Media"), Key("Workers.Env", "Vectorize"), Key("Workers.Env", "SecretsStoreSecret"),
        Key("Workers.Env", "Hyperdrive"),
        Key("Workers.CacheStorage", "OpenAsync"), Key("Workers.Http", "FetchAsync"), Key("Workers.Socket", "Connect"),
        Key("Workers.Performance", "Now"), Key("Workers.Scheduler", "WaitAsync"),
        Key("Workers.Timers", "SetTimeout"), Key("Workers.Timers", "ClearTimeout"),
        Key("Workers.Timers", "SetInterval"), Key("Workers.Timers", "ClearInterval"),
        Key("Workers.Response", "Json"), Key("Workers.Response", "Redirect"), Key("Workers.Response", "Error"),
        Key("Workers.ResponseCookies", "Set"), Key("Workers.ResponseCookies", "Delete"),
        Key("Workers.Cookies", "Get"), Key("Workers.Cookies", "GetAll"), Key("Workers.Cookies", "Has"),
        Key("Workers.Router", "Get"), Key("Workers.Router", "Post"), Key("Workers.Router", "Put"),
        Key("Workers.Router", "Patch"), Key("Workers.Router", "Delete"), Key("Workers.Router", "Options"),
        Key("Workers.Router", "Any"), Key("Workers.Router", "Fallback"), Key("Workers.Router", "HandleAsync"),
        Key("Workers.RouteContext", "Parameter"),
        Key("Workers.Router", "Use"), Key("Workers.Router", "OnError"), Key("Workers.Cors", "Allow"),
        Key("Workers.Numbers", "From"), Key("Workers.Numbers", "ParseInt"), Key("Workers.Numbers", "ParseFloat"),
        Key("Workers.Numbers", "IsNaN"), Key("Workers.Numbers", "IsFinite"), Key("Workers.Numbers", "IsInteger"),
        Key("Workers.Numbers", "IsSafeInteger"), Key("Workers.Numbers", "ToFixed"), Key("Workers.Numbers", "ToPrecision"),
        Key("Workers.Numbers", "ToRadixString"), Key("Workers.Numbers", "Abs"), Key("Workers.Numbers", "Round"),
        Key("Workers.Numbers", "Floor"), Key("Workers.Numbers", "Ceil"), Key("Workers.Numbers", "Trunc"),
        Key("Workers.Numbers", "Min"), Key("Workers.Numbers", "Max"), Key("Workers.Numbers", "Pow"),
        Key("Workers.Numbers", "Sqrt"), Key("Workers.Numbers", "Log"), Key("Workers.Numbers", "Exp"),
        Key("Workers.Numbers", "Sin"), Key("Workers.Numbers", "Cos"), Key("Workers.Numbers", "Tan"),
        Key("Workers.Numbers", "Sign"), Key("Workers.Numbers", "Random"),
        Key("Workers.Url", "EncodeComponent"), Key("Workers.Url", "DecodeComponent"),
        Key("Workers.Url", "CanParse"), Key("Workers.Url", "Parse"),
        Key("Workers.Json", "Parse"), Key("Workers.Json", "Stringify"),
        Key("Workers.JsonValue", "From"), Key("Workers.JsonValue", "AsString"), Key("Workers.JsonValue", "AsNumber"),
        Key("Workers.JsonValue", "AsBoolean"), Key("Workers.JsonValue", "Has"), Key("Workers.JsonValue", "Values"),
        Key("Workers.JsonValue", "As"),
        Key("Workers.Date", "Now"), Key("Workers.Date", "Parse"), Key("Workers.Date", "Utc"), Key("Workers.Date", "GetTime"),
        Key("Workers.Date", "GetUtcFullYear"), Key("Workers.Date", "GetUtcMonth"), Key("Workers.Date", "GetUtcDate"),
        Key("Workers.Date", "GetUtcDay"), Key("Workers.Date", "GetUtcHours"), Key("Workers.Date", "GetUtcMinutes"),
        Key("Workers.Date", "GetUtcSeconds"), Key("Workers.Date", "GetUtcMilliseconds"), Key("Workers.Date", "SetTime"),
        Key("Workers.Date", "SetUtcFullYear"), Key("Workers.Date", "SetUtcMonth"), Key("Workers.Date", "SetUtcDate"),
        Key("Workers.Date", "SetUtcHours"), Key("Workers.Date", "SetUtcMinutes"), Key("Workers.Date", "SetUtcSeconds"),
        Key("Workers.Date", "SetUtcMilliseconds"), Key("Workers.Date", "ToIsoString"), Key("Workers.Date", "ToJson"),
        Key("Workers.Date", "ToUtcString"),
        Key("Workers.AbortController", "Abort"), Key("Workers.AbortSignal", "Abort"), Key("Workers.AbortSignal", "Timeout"),
        Key("Workers.AbortSignal", "Any"), Key("Workers.AbortSignal", "ThrowIfAborted"),
        Key("Workers.RegExp", "Test"), Key("Workers.RegExp", "Exec"), Key("Workers.RegExp", "Replace"), Key("Workers.RegExp", "Split"),
        Key("Workers.Log", "Info"), Key("Workers.Log", "Debug"), Key("Workers.Log", "Warn"), Key("Workers.Log", "Error"),
        Key("Workers.Crypto", "RandomUuid"),
        Key("Workers.Base64", "Encode"), Key("Workers.Base64", "Decode"), Key("Workers.Hex", "Encode"), Key("Workers.Hex", "Decode")
    ];

    // Enumerators are not called directly: foreach lowers these collections to their native iteration.
    private static readonly HashSet<(string Type, string Method)> ForeachEnumerators =
    [
        Key("Workers.Headers", "GetEnumerator"), Key("Workers.UrlSearchParams", "GetEnumerator"),
        Key("Workers.FormData", "GetEnumerator"), Key("Workers.Cookies", "GetEnumerator")
    ];

    // Handler base classes are subclassed by Workers code and emitted as JavaScript classes; the runtime calls
    // their overridden handler methods.
    private static readonly HashSet<string> HandlerBaseTypes =
    [
        "Workers.HtmlElementHandler", "Workers.HtmlDocumentHandler", "Workers.DurableObject", "Workers.WorkerEntrypoint"
    ];

    public static bool IsStructural(string type) => StructuralTypes.Contains(type);

    internal static IEnumerable<(string Type, string Method)> ClassifiedMethods => SpecialMethods.Concat(ForeachEnumerators);
    internal static IEnumerable<string> ClassifiedTypes => StructuralTypes.Concat(HandlerBaseTypes);

    public static bool IsClassified((string Type, string Method) key, bool hasIntrinsic) =>
        hasIntrinsic || SpecialMethods.Contains(key) || ForeachEnumerators.Contains(key) || HandlerBaseTypes.Contains(key.Type);
}
