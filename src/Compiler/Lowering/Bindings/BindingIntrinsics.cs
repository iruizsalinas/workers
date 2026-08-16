using Microsoft.CodeAnalysis;

internal enum BindingIntrinsicKind
{
    Direct,
    KvBytesGet,
    KvJsonGet,
    KvJsonPut,
    DurableObjectGet,
    CacheQuery,
    CacheMatch,
    ServiceRpc,
    Property,
    Identity,
    HeadersClone,
    Fluent,
    JsonParse,
    Dispose,
    RateLimit,
    CryptoRandomBytes,
    CryptoTimingSafeEqual,
    CryptoDigestText,
    CryptoDigest,
    CryptoDigestBody,
    CryptoDigestStream,
    DigestWrite,
    DigestWriteText,
    DigestClose,
    DigestResult,
    ReadableFromEnumerable,
    ReadableRead,
    ReadableAll,
    WebSocketEvents,
    SocketRead,
    SocketWrite,
    SocketWriteText,
    SocketCloseWritable,
    BodyText,
    BodyJson,
    WebSocketJson,
    WebSocketMessageText,
    Bytes,
    CryptoVerifyHmac,
    BlobSliceBytes,
    QueryNames,
    CompressStream,
    DecompressStream
}

internal sealed record BindingIntrinsic(string JavascriptName, BindingIntrinsicKind Kind = BindingIntrinsicKind.Direct);

/// <summary>
/// The complete set of Workers binding calls that may cross directly into the
/// Cloudflare JavaScript runtime. Keeping this symbol keyed prevents an
/// unrelated SDK or user method with a familiar name from being rewritten.
/// </summary>
internal static class BindingIntrinsicRegistry
{
    private static readonly IReadOnlyList<IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic>> MethodSets =
    [
        KvIntrinsics.Methods,
        R2Intrinsics.Methods,
        D1Intrinsics.Methods,
        QueueIntrinsics.Methods,
        NetworkIntrinsics.Methods,
        DurableObjectIntrinsics.Methods,
        PlatformIntrinsics.Methods,
        RuntimeIntrinsics.Methods
    ];

    public static bool TryGet(IMethodSymbol method, out BindingIntrinsic intrinsic)
    {
        var key = Key(TypeName(method.OriginalDefinition.ContainingType), method.OriginalDefinition.Name);
        foreach (var methods in MethodSets)
            if (methods.TryGetValue(key, out intrinsic!))
                return true;

        intrinsic = null!;
        return false;
    }

    public static bool IsStructuralType(INamedTypeSymbol type) => BindingClassification.IsStructural(TypeName(type.OriginalDefinition));

    public static bool IsQueueMessageBatch(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && TypeName(named.OriginalDefinition) == "Workers.QueueMessageBatch<T>";

    public static bool IsClassified(IMethodSymbol method)
    {
        var key = Key(TypeName(method.OriginalDefinition.ContainingType), method.OriginalDefinition.Name);
        return BindingClassification.IsClassified(key, HasMethod(key));
    }

    internal static bool IsClassified(string containingType, string methodName)
    {
        var key = Key(containingType, methodName);
        return BindingClassification.IsClassified(key, HasMethod(key));
    }

    private static bool HasMethod((string Type, string Method) key) => MethodSets.Any(methods => methods.ContainsKey(key));
    internal static BindingIntrinsic Direct(string name) => new(name);
    internal static (string Type, string Method) Key(string type, string method) => (type, method);
    private static string TypeName(INamedTypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
