using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Workers.Compiler.Tests;

// Checks the Workers API against the runtime declarations that `wrangler types` generates from the
// installed workerd, so a mapping to a member the runtime does not have fails here, not in production.
public sealed partial class RuntimeApiConformanceTests
{
    private static readonly Lazy<string> RuntimeTypes = new(GenerateRuntimeTypes);

    // Workers API types and the runtime declarations that implement them.
    private static readonly Dictionary<string, string> RuntimeTypeNames = new()
    {
        ["Workers.ExecutionContext"] = "ExecutionContext",
        ["Workers.CacheContext"] = "CacheContext",
        ["Workers.CachePurgeOptions"] = "CachePurgeOptions",
        ["Workers.CachePurgeResult"] = "CachePurgeResult",
        ["Workers.CachePurgeError"] = "CachePurgeError",
        ["Workers.CloudflareAccessContext"] = "CloudflareAccessContext",
        ["Workers.CloudflareAccessIdentity"] = "CloudflareAccessIdentity",
        ["Workers.Tracing"] = "Tracing",
        ["Workers.Span"] = "Span",
        ["Workers.TracingSpanStatus"] = "TracingSpanStatus",
        ["Workers.ScheduledController"] = "ScheduledController",
        ["Workers.MessageBatch<T>"] = "MessageBatch",
        ["Workers.MessageBatchMetadata"] = "MessageBatchMetadata",
        ["Workers.Message<T>"] = "Message",
        ["Workers.ForwardableEmailMessage"] = "ForwardableEmailMessage",
        ["Workers.TraceItem"] = "TraceItem",
        ["Workers.TraceItemFetchEventInfo"] = "TraceItemFetchEventInfo",
        ["Workers.TraceItemFetchEventInfoRequest"] = "TraceItemFetchEventInfoRequest",
        ["Workers.TraceItemFetchEventInfoResponse"] = "TraceItemFetchEventInfoResponse",
        ["Workers.TraceLog"] = "TraceLog",
        ["Workers.TraceLogErrorInfo"] = "TraceLogErrorInfo",
        ["Workers.ScriptVersion"] = "ScriptVersion",
        ["Workers.TraceException"] = "TraceException",
        ["Workers.AlarmInvocationInfo"] = "AlarmInvocationInfo",
        ["Workers.Request"] = "Request",
        ["Workers.Response"] = "Response",
        ["Workers.Headers"] = "Headers",
        ["Workers.Url"] = "URL",
        ["Workers.UrlSearchParams"] = "URLSearchParams",
        ["Workers.FormData"] = "FormData",
        ["Workers.Blob"] = "Blob",
        ["Workers.File"] = "File",
        ["Workers.IncomingRequestCfProperties"] = "IncomingRequestCfPropertiesBase|IncomingRequestCfPropertiesGeographicInformation|IncomingRequestCfPropertiesBotManagement",
        ["Workers.IncomingRequestCfPropertiesBotManagementBase"] = "IncomingRequestCfPropertiesBotManagementBase",
        ["Workers.IncomingRequestCfPropertiesExportedAuthenticatorMetadata"] = "IncomingRequestCfPropertiesExportedAuthenticatorMetadata",
        ["Workers.ReadableStream"] = "ReadableStream",
        ["Workers.ReadableStreamDefaultReader"] = "ReadableStreamDefaultReader",
        ["Workers.WritableStream"] = "WritableStream",
        ["Workers.WritableStreamDefaultWriter"] = "WritableStreamDefaultWriter",
        ["Workers.TransformStream"] = "TransformStream",
        ["Workers.IdentityTransformStream"] = "IdentityTransformStream",
        ["Workers.FixedLengthStream"] = "FixedLengthStream",
        ["Workers.CompressionStream"] = "CompressionStream",
        ["Workers.DecompressionStream"] = "DecompressionStream",
        ["Workers.TextEncoder"] = "TextEncoder",
        ["Workers.TextDecoder"] = "TextDecoder",
        ["Workers.DigestStream"] = "DigestStream",
        ["Workers.Crypto"] = "Crypto",
        ["Workers.SubtleCrypto"] = "SubtleCrypto",
        ["Workers.CryptoKey"] = "CryptoKey",
        ["Workers.CryptoKeyPair"] = "CryptoKeyPair",
        ["Workers.CryptoKeyAlgorithm"] = "CryptoKeyKeyAlgorithm|CryptoKeyAesKeyAlgorithm|CryptoKeyHmacKeyAlgorithm|CryptoKeyRsaKeyAlgorithm|CryptoKeyEllipticKeyAlgorithm|CryptoKeyArbitraryKeyAlgorithm",
        ["Workers.JsonWebKey"] = "JsonWebKey",
        ["Workers.EncapsulatedBits"] = "SubtleCryptoEncapsulatedBits",
        ["Workers.EncapsulatedKey"] = "SubtleCryptoEncapsulatedKey",
        ["Workers.AesGcmParams"] = "SubtleCryptoEncryptAlgorithm",
        ["Workers.AesCbcParams"] = "SubtleCryptoEncryptAlgorithm",
        ["Workers.AesCtrParams"] = "SubtleCryptoEncryptAlgorithm",
        ["Workers.AesKwParams"] = "SubtleCryptoEncryptAlgorithm",
        ["Workers.RsaOaepParams"] = "SubtleCryptoEncryptAlgorithm",
        ["Workers.HmacParams"] = "SubtleCryptoSignAlgorithm",
        ["Workers.RsaPkcs1Params"] = "SubtleCryptoSignAlgorithm",
        ["Workers.RsaPssParams"] = "SubtleCryptoSignAlgorithm",
        ["Workers.EcdsaParams"] = "SubtleCryptoSignAlgorithm",
        ["Workers.Ed25519Params"] = "SubtleCryptoSignAlgorithm",
        ["Workers.NodeEd25519Params"] = "SubtleCryptoSignAlgorithm",
        ["Workers.MlDsaParams"] = "SubtleCryptoSignAlgorithm",
        ["Workers.X25519Params"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.MlKemParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.AesKeyParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.HmacKeyParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.RsaKeyGenParams"] = "SubtleCryptoGenerateKeyAlgorithm",
        ["Workers.RsaKeyImportParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.EcKeyParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.Pbkdf2KeyParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.HkdfKeyParams"] = "SubtleCryptoImportKeyAlgorithm",
        ["Workers.Pbkdf2Params"] = "SubtleCryptoDeriveKeyAlgorithm",
        ["Workers.HkdfParams"] = "SubtleCryptoDeriveKeyAlgorithm",
        ["Workers.EcdhKeyDeriveParams"] = "SubtleCryptoDeriveKeyAlgorithm",
        ["Workers.DigestStreamOptions"] = "DigestStreamOptions",
        ["Workers.Socket"] = "Socket",
        ["Workers.SocketInfo"] = "SocketInfo",
        ["Workers.SocketAddress"] = "SocketAddress",
        ["Workers.SocketOptions"] = "SocketOptions",
        ["Workers.WebSocket"] = "WebSocket",
        ["Workers.CloseEvent"] = "CloseEvent",
        ["Workers.ErrorEvent"] = "ErrorEvent",
        ["Workers.WebSocketRequestResponsePair"] = "WebSocketRequestResponsePair",
        ["Workers.HtmlRewriter"] = "HTMLRewriter",
        ["Workers.HtmlElement"] = "Element",
        ["Workers.HtmlEndTag"] = "EndTag",
        ["Workers.HtmlText"] = "Text",
        ["Workers.HtmlComment"] = "Comment",
        ["Workers.HtmlDoctype"] = "Doctype",
        ["Workers.HtmlDocumentEnd"] = "DocumentEnd",
        ["Workers.HtmlContentOptions"] = "ContentOptions",
        ["Workers.Cache"] = "Cache",
        ["Workers.CacheQueryOptions"] = "CacheQueryOptions",
        ["Workers.RequestInit"] = "RequestInit",
        ["Workers.RequestInitCfProperties"] = "RequestInitCfProperties",
        ["Workers.RequestInitCfPropertiesR2"] = "RequestInitCfPropertiesR2",
        ["Workers.RequestInitCfPropertiesVary"] = "RequestInitCfPropertiesVary",
        ["Workers.RequestInitCfPropertiesVaryHeader"] = "RequestInitCfPropertiesVaryAcceptHeader|RequestInitCfPropertiesVaryAcceptLanguageHeader",
        ["Workers.StreamPipeOptions"] = "StreamPipeOptions",
        ["Workers.Fetcher"] = "Fetcher",
        ["Workers.DispatchNamespace"] = "DispatchNamespace",
        ["Workers.KVNamespace"] = "KVNamespace",
        ["Workers.KVNamespaceGetOptions"] = "KVNamespaceGetOptions",
        ["Workers.KVNamespacePutOptions"] = "KVNamespacePutOptions",
        ["Workers.KVNamespaceListOptions"] = "KVNamespaceListOptions",
        ["Workers.KVNamespaceListResult"] = "KVNamespaceListResult",
        ["Workers.KVNamespaceListKey"] = "KVNamespaceListKey",
        ["Workers.KVNamespaceGetWithMetadataResult<T>"] = "KVNamespaceGetWithMetadataResult",
        ["Workers.R2Bucket"] = "R2Bucket",
        ["Workers.R2Object"] = "R2Object",
        ["Workers.R2ObjectBody"] = "R2ObjectBody",
        ["Workers.R2Objects"] = "R2Objects",
        ["Workers.R2Checksums"] = "R2Checksums",
        ["Workers.R2UploadPartOptions"] = "R2UploadPartOptions",
        ["Workers.R2GetOptions"] = "R2GetOptions",
        ["Workers.R2PutOptions"] = "R2PutOptions",
        ["Workers.R2MultipartOptions"] = "R2MultipartOptions",
        ["Workers.R2ListOptions"] = "R2ListOptions",
        ["Workers.R2Conditional"] = "R2Conditional",
        ["Workers.R2HttpMetadata"] = "R2HTTPMetadata",
        ["Workers.R2MultipartUpload"] = "R2MultipartUpload",
        ["Workers.R2UploadedPart"] = "R2UploadedPart",
        ["Workers.D1Database"] = "D1Database",
        ["Workers.D1DatabaseSession"] = "D1DatabaseSession",
        ["Workers.D1PreparedStatement"] = "D1PreparedStatement",
        ["Workers.D1Result<T>"] = "D1Result|D1Response",
        ["Workers.D1ExecResult"] = "D1ExecResult",
        ["Workers.D1Meta"] = "D1Meta",
        ["Workers.Queue"] = "Queue",
        ["Workers.QueueSendOptions"] = "QueueSendOptions",
        ["Workers.QueueSendBatchOptions"] = "QueueSendBatchOptions",
        ["Workers.MessageSendRequest<T>"] = "MessageSendRequest",
        ["Workers.QueueRetryOptions"] = "QueueRetryOptions",
        ["Workers.QueueMetrics"] = "QueueMetrics",
        ["Workers.QueueSendResponse"] = "QueueSendResponse",
        ["Workers.QueueSendBatchResponse"] = "QueueSendBatchResponse",
        ["Workers.QueueSendMetadata"] = "QueueSendMetadata|QueueSendBatchMetadata",
        ["Workers.DurableObjectNamespace"] = "DurableObjectNamespace",
        ["Workers.DurableObjectNamespaceNewUniqueIdOptions"] = "DurableObjectNamespaceNewUniqueIdOptions",
        ["Workers.DurableObjectNamespaceGetDurableObjectOptions"] = "DurableObjectNamespaceGetDurableObjectOptions",
        ["Workers.DurableObjectId"] = "DurableObjectId",
        ["Workers.DurableObjectStub"] = "DurableObjectStub",
        ["Workers.DurableObjectState"] = "DurableObjectState",
        ["Workers.DurableObjectAbortOptions"] = "DurableObjectAbortOptions",
        ["Workers.DurableObjectGetAlarmOptions"] = "DurableObjectGetAlarmOptions",
        ["Workers.DurableObjectSetAlarmOptions"] = "DurableObjectSetAlarmOptions",
        ["Workers.DurableObjectStorage"] = "DurableObjectStorage",
        ["Workers.DurableObjectTransaction"] = "DurableObjectTransaction",
        ["Workers.DurableObjectGetOptions"] = "DurableObjectGetOptions",
        ["Workers.DurableObjectPutOptions"] = "DurableObjectPutOptions",
        ["Workers.DurableObjectListOptions"] = "DurableObjectListOptions",
        ["Workers.SyncKvStorage"] = "SyncKvStorage",
        ["Workers.SyncKvListOptions"] = "SyncKvListOptions",
        ["Workers.SqlStorage"] = "SqlStorage",
        ["Workers.SqlStorageCursor<T>"] = "SqlStorageCursor",
        ["Workers.Container"] = "Container",
        ["Workers.ContainerStartupOptions"] = "ContainerStartupOptions",
        ["Workers.ContainerStartResources"] = "ContainerStartResources",
        ["Workers.ContainerSnapshotOptions"] = "ContainerSnapshotOptions",
        ["Workers.ContainerSnapshot"] = "ContainerSnapshot",
        ["Workers.ContainerSnapshotRestoreParams"] = "ContainerSnapshotRestoreParams",
        ["Workers.ContainerInfo"] = "ContainerInfo",
        ["Workers.ContainerExecOptions"] = "ContainerExecOptions",
        ["Workers.ContainerExecPtyOptions"] = "ContainerExecPtyOptions",
        ["Workers.ExecProcess"] = "ExecProcess",
        ["Workers.ExecOutput"] = "ExecOutput",
        ["Workers.RateLimit"] = "RateLimit",
        ["Workers.RateLimitOptions"] = "RateLimitOptions",
        ["Workers.RateLimitOutcome"] = "RateLimitOutcome",
        ["Workers.AnalyticsEngineDataset"] = "AnalyticsEngineDataset",
        ["Workers.AnalyticsEngineDataPoint"] = "AnalyticsEngineDataPoint",
        ["Workers.SendEmail"] = "SendEmail",
        ["Workers.EmailMessage"] = "EmailMessage",
        ["Workers.EmailMessageBuilder"] = "EmailReplyMessageBuilder|EmailDestinations",
        ["Workers.EmailReplyMessageBuilder"] = "EmailReplyMessageBuilder",
        ["Workers.EmailAddress"] = "EmailAddress",
        ["Workers.EmailAttachment"] = "EmailAttachment",
        ["Workers.EmailSendResult"] = "EmailSendResult",
        ["Workers.Ai"] = "Ai",
        ["Workers.AiOptions"] = "AiOptions",
        ["Workers.GatewayOptions"] = "GatewayOptions",
        ["Workers.GatewayRetries"] = "GatewayRetries",
        ["Workers.AiWebSearchRequest"] = "AiWebSearchRequest",
        ["Workers.AiGateway"] = "AiGateway",
        ["Workers.AiGatewayPatchLog"] = "AiGatewayPatchLog",
        ["Workers.AiGatewayLog"] = "AiGatewayLog",
        ["Workers.AIGatewayUniversalRequest"] = "AIGatewayUniversalRequest",
        ["Workers.UniversalGatewayOptions"] = "GatewayOptions",
        ["Workers.AiModelsSearchParams"] = "AiModelsSearchParams",
        ["Workers.AiModelsSearchObject"] = "AiModelsSearchObject",
        ["Workers.ToMarkdownService"] = "ToMarkdownService",
        ["Workers.SupportedFileFormat"] = "SupportedFileFormat",
        ["Workers.MarkdownDocument"] = "MarkdownDocument",
        ["Workers.ConversionRequestOptions"] = "ConversionRequestOptions",
        ["Workers.ConversionOptions"] = "ConversionOptions",
        ["Workers.ConversionOutputOptions"] = "ConversionOutputOptions",
        ["Workers.ImageConversionOptions"] = "ImageConversionOptions",
        ["Workers.EmbeddedImageConversionOptions"] = "EmbeddedImageConversionOptions|ImageConversionOptions",
        ["Workers.ConversionResponse"] = "ConversionResponse",
        ["Workers.Workflow"] = "Workflow",
        ["Workers.WorkflowInstance"] = "WorkflowInstance",
        ["Workers.WorkflowInstanceCreateOptions"] = "WorkflowInstanceCreateOptions",
        ["Workers.WorkflowBatchCreateOptions"] = "WorkflowBatchCreateOptions",
        ["Workers.WorkflowBatchCreateResult"] = "WorkflowBatchCreateResult",
        ["Workers.WorkflowBatchDeleteResult"] = "WorkflowBatchDeleteResult",
        ["Workers.WorkflowInstanceSubscribeOptions"] = "WorkflowInstanceSubscribeOptions",
        ["Workers.WorkflowInstanceSubscription"] = "WorkflowInstanceSubscription",
        ["Workers.WorkflowInstanceEvent"] = "WorkflowInstanceEvent",
        ["Workers.WorkflowInstanceTerminateOptions"] = "WorkflowInstanceTerminateOptions",
        ["Workers.WorkflowInstanceRestartOptions"] = "WorkflowInstanceRestartOptions",
        ["Workers.InstanceStatus"] = "InstanceStatus",
        ["Workers.ImagesBinding"] = "ImagesBinding",
        ["Workers.ImageTransformer"] = "ImageTransformer",
        ["Workers.ImageTransformationResult"] = "ImageTransformationResult",
        ["Workers.ImageOutputOptions"] = "ImageOutputOptions",
        ["Workers.ImageInputOptions"] = "ImageInputOptions",
        ["Workers.ImageTextOptions"] = "TextOptions",
        ["Workers.ImageTransformationResponseOptions"] = "ImageTransformationResponseOptions",
        ["Workers.ImageTransformationOutputOptions"] = "ImageTransformationOutputOptions",
        ["Workers.MediaBinding"] = "MediaBinding",
        ["Workers.MediaTransformer"] = "MediaTransformer",
        ["Workers.MediaTransformationGenerator"] = "MediaTransformationGenerator",
        ["Workers.MediaTransformationResult"] = "MediaTransformationResult",
        ["Workers.MediaTransformationOutputOptions"] = "MediaTransformationOutputOptions",
        ["Workers.Vectorize"] = "Vectorize",
        ["Workers.VectorizeVector"] = "VectorizeVector",
        ["Workers.VectorizeQueryOptions"] = "VectorizeQueryOptions",
        ["Workers.VectorizeMatches"] = "VectorizeMatches",
        ["Workers.VectorizeAsyncMutation"] = "VectorizeAsyncMutation",
        ["Workers.VectorizeIndexInfo"] = "VectorizeIndexInfo",
        ["Workers.SecretsStoreSecret"] = "SecretsStoreSecret",
        ["Workers.Hyperdrive"] = "Hyperdrive",
        ["Workers.WorkerVersionMetadata"] = "WorkerVersionMetadata",
        ["Workers.RpcStub"] = "StubBase"
    };

    // Intrinsic kinds whose JavaScript name is a member of the receiver.
    private static readonly HashSet<BindingIntrinsicKind> MemberKinds =
    [
        BindingIntrinsicKind.Direct, BindingIntrinsicKind.Property, BindingIntrinsicKind.Fluent,
        BindingIntrinsicKind.RequestBodyRead, BindingIntrinsicKind.KvJsonPut, BindingIntrinsicKind.ArrayArguments, BindingIntrinsicKind.R2Get,
        BindingIntrinsicKind.DurableObjectGet, BindingIntrinsicKind.SyncStorageGet, BindingIntrinsicKind.CacheMatch,
        BindingIntrinsicKind.DictionaryObject, BindingIntrinsicKind.DateResult, BindingIntrinsicKind.IteratorArray,
        BindingIntrinsicKind.CryptoBytesResult, BindingIntrinsicKind.CryptoImportKey, BindingIntrinsicKind.CryptoExportJwk
    ];

    // Receivers whose members are not a fixed runtime interface.
    private static readonly HashSet<string> OpenReceivers = ["Workers.RawBinding"];

    // Properties whose runtime declaration differs from the name the compiler emits. Every other rename comes from
    // the compiler's NativePropertyNames table, so the test checks the names the compiler actually uses.
    private static readonly Dictionary<string, string> DeclarationNames = new()
    {
        // Runtime declarations retain the C++ identifier, but the JS dictionary field is public.
        ["Workers.EcdhKeyDeriveParams.Public"] = "$public"
    };

    // Properties the compiler computes instead of reading a runtime member of the same name.
    private static readonly HashSet<string> ComputedProperties =
    [
        "Workers.Request.Cookies", "Workers.Response.Cookies", "Workers.Headers.Item", "Workers.CloudflareAccessIdentity.Item"
    ];

    // Properties declared only in the union branches of a runtime type (`{ ... } & ({ ... } | { ... })`),
    // which the declaration scan skips.
    private static readonly HashSet<string> UnionBranchProperties =
    [
        "Workers.R2Objects.Cursor", "Workers.R2Objects.Truncated",
        "Workers.ConversionResponse.Error",
        // convertOGImage is declared inline on the HTML image options rather than on EmbeddedImageConversionOptions.
        "Workers.EmbeddedImageConversionOptions.ConvertOGImage",
        "Workers.ContainerStartupOptions.Image", "Workers.ContainerStartupOptions.ContainerSnapshot",
        "Workers.WorkflowInstanceEvent.Type", "Workers.WorkflowInstanceEvent.StepName", "Workers.WorkflowInstanceEvent.Attempt",
        "Workers.WorkflowInstanceEvent.Params", "Workers.WorkflowInstanceEvent.Output", "Workers.WorkflowInstanceEvent.Error",
        "Workers.WorkflowInstanceEvent.RetryDelayMs", "Workers.WorkflowInstanceEvent.DurationMs",
        "Workers.WorkflowInstanceEvent.EventType"
    ];

    [Fact]
    public void IntrinsicsCallMembersTheRuntimeDeclares()
    {
        var missing = global::BindingIntrinsicRegistry.Entries
            .Where(entry => MemberKinds.Contains(entry.Value.Kind) && !OpenReceivers.Contains(entry.Key.Type))
            .Where(entry => !HasRuntimeMember(entry.Key.Type, entry.Value.JavascriptName))
            .Select(entry => $"{entry.Key.Type}.{entry.Key.Method} -> {entry.Value.JavascriptName}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "Mapped members the runtime does not declare:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void PropertiesReadMembersTheRuntimeDeclares()
    {
        var missing = typeof(global::Workers.Response).Assembly.GetExportedTypes()
            .Select(type => (Type: type, Name: ApiTypeName(type)))
            .Where(type => RuntimeTypeNames.ContainsKey(type.Name))
            .SelectMany(type => type.Type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.CanRead)
                .Select(property => (type.Name, Property: property.Name)))
            .Where(item => !ComputedProperties.Contains($"{item.Name}.{item.Property}")
                && !UnionBranchProperties.Contains($"{item.Name}.{item.Property}"))
            .Where(item => !HasRuntimeMember(item.Name, DeclarationNames.GetValueOrDefault($"{item.Name}.{item.Property}")
                ?? global::NativePropertyNames.Get(item.Name, item.Property)
                ?? char.ToLowerInvariant(item.Property[0]) + item.Property[1..]))
            .Select(item => $"{item.Name}.{item.Property}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "Properties the runtime does not declare:\n" + string.Join("\n", missing));
    }

    [Theory]
    [InlineData("Workers.Request", "json", true)]
    [InlineData("Workers.R2ObjectBody", "key", true)]
    [InlineData("Workers.DigestStream", "getWriter", true)]
    [InlineData("Workers.ExecProcess", "getExitCode", false)]
    [InlineData("Workers.RpcStub", "duplicate", false)]
    public void TheCheckFollowsInheritanceAndRejectsUndeclaredMembers(string apiType, string member, bool declared)
    {
        Assert.Equal(declared, HasRuntimeMember(apiType, member));
    }

    [Fact]
    public void EveryReceiverWithRuntimeMembersHasADeclaration()
    {
        var unmapped = global::BindingIntrinsicRegistry.Entries
            .Where(entry => MemberKinds.Contains(entry.Value.Kind) && !OpenReceivers.Contains(entry.Key.Type))
            .Select(entry => entry.Key.Type)
            .Where(type => !RuntimeTypeNames.ContainsKey(type))
            .Distinct()
            .ToArray();

        Assert.True(unmapped.Length == 0, "Add a runtime declaration for:\n" + string.Join("\n", unmapped));
    }

    private static bool HasRuntimeMember(string apiType, string member)
    {
        if (!RuntimeTypeNames.TryGetValue(apiType, out var runtimeType)) return false;
        var pattern = new Regex(@"^\s*(?:(?:get|set|readonly|static|public|abstract)\s+)*" + Regex.Escape(member) + @"\s*[\?\(<:]",
            RegexOptions.Multiline);
        return runtimeType.Split('|').SelectMany(type => Declarations(type, [])).Any(block => pattern.IsMatch(block));
    }

    // The bodies of every declaration of a runtime type, including the types it extends.
    private static IEnumerable<string> Declarations(string name, HashSet<string> visited)
    {
        if (!visited.Add(name)) yield break;
        var source = RuntimeTypes.Value;
        foreach (Match match in DeclarationPattern(name).Matches(source))
        {
            // Skip the type's own generic parameters, which can contain nested '<' and 'extends'.
            var position = match.Index + match.Length;
            if (position < source.Length && source[position] == '<')
                for (var depth = 0; position < source.Length; position++)
                {
                    if (source[position] == '<') depth++;
                    else if (source[position] == '>' && --depth == 0) { position++; break; }
                }
            var open = source.IndexOf('{', position);
            var header = source[position..open];
            var end = open;
            for (var depth = 0; end < source.Length; end++)
            {
                if (source[end] == '{') depth++;
                else if (source[end] == '}' && --depth == 0) break;
            }
            yield return source[open..end];
            var extends = header.IndexOf(" extends ", StringComparison.Ordinal);
            if (extends < 0) continue;
            foreach (Match parent in ParentPattern().Matches(header[(extends + 9)..]))
                foreach (var inherited in Declarations(parent.Value, visited))
                    yield return inherited;
        }
    }

    private static Regex DeclarationPattern(string name) => new(
        @"^\s*(?:export\s+)?(?:declare\s+)?(?:abstract\s+)?(?:interface|class|type)\s+" + Regex.Escape(name) + @"\b",
        RegexOptions.Multiline);

    // Top-level type names in an extends clause, ignoring their type arguments.
    [GeneratedRegex(@"(?<![<\w.])[A-Z]\w*")]
    private static partial Regex ParentPattern();

    private static string ApiTypeName(Type type) => type.IsGenericTypeDefinition
        ? $"{type.FullName![..type.FullName!.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>"
        : type.FullName!;

    private static string GenerateRuntimeTypes()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !System.IO.File.Exists(Path.Combine(repository.FullName, "Workers.slnx")))
            repository = repository.Parent;
        var wrangler = Path.Combine(repository?.FullName ?? "", "node_modules", "wrangler", "bin", "wrangler.js");
        if (!System.IO.File.Exists(wrangler))
            throw new InvalidOperationException("Run 'npm ci' first: the runtime declarations come from the installed wrangler.");

        var directory = Directory.CreateTempSubdirectory("workers-runtime-types-");
        try
        {
            System.IO.File.WriteAllText(Path.Combine(directory.FullName, "wrangler.jsonc"),
                """{ "name": "runtime-types", "main": "worker.js", "compatibility_date": "2026-10-11", "compatibility_flags": ["webcrypto_modern_algorithms"] }""");
            System.IO.File.WriteAllText(Path.Combine(directory.FullName, "worker.js"), "export default {};");
            var start = new ProcessStartInfo("node")
            {
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { wrangler, "types", "--path", "runtime.d.ts" })
                start.ArgumentList.Add(argument);
            start.Environment["WRANGLER_SEND_METRICS"] = "false";
            start.Environment["WRANGLER_WRITE_LOGS"] = "false";
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start node.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"wrangler types failed:{Environment.NewLine}{output.Result}{error}");
            return System.IO.File.ReadAllText(Path.Combine(directory.FullName, "runtime.d.ts")).ReplaceLineEndings("\n");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
