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
        ["Workers.Context"] = "ExecutionContext",
        ["Workers.Request"] = "Request",
        ["Workers.Response"] = "Response",
        ["Workers.Headers"] = "Headers",
        ["Workers.QueryParameters"] = "URLSearchParams",
        ["Workers.ReadableStream"] = "ReadableStream",
        ["Workers.ReadableStreamReader"] = "ReadableStreamDefaultReader",
        ["Workers.WritableStream"] = "WritableStream",
        ["Workers.WritableStreamWriter"] = "WritableStreamDefaultWriter",
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
        ["Workers.TcpSocket"] = "Socket",
        ["Workers.WebSocket"] = "WebSocket",
        ["Workers.ScheduledEvent"] = "ScheduledController",
        ["Workers.QueueMessage<T>"] = "Message",
        ["Workers.QueueMessageBatch<T>"] = "MessageBatch",
        ["Workers.ForwardableEmailMessage"] = "ForwardableEmailMessage",
        ["Workers.HtmlRewriter"] = "HTMLRewriter",
        ["Workers.HtmlElement"] = "Element",
        ["Workers.HtmlEndTag"] = "EndTag",
        ["Workers.HtmlTextChunk"] = "Text",
        ["Workers.HtmlComment"] = "Comment",
        ["Workers.HtmlDocumentEnd"] = "DocumentEnd",
        ["Workers.ICache"] = "Cache",
        ["Workers.IFetcherBinding"] = "Fetcher",
        ["Workers.IKvNamespace"] = "KVNamespace",
        ["Workers.IR2Bucket"] = "R2Bucket",
        ["Workers.R2ObjectBody"] = "R2ObjectBody",
        ["Workers.R2MultipartUpload"] = "R2MultipartUpload",
        ["Workers.ID1Database"] = "D1Database",
        ["Workers.D1PreparedStatement"] = "D1PreparedStatement",
        ["Workers.D1DatabaseSession"] = "D1DatabaseSession",
        ["Workers.IQueueProducer"] = "Queue",
        ["Workers.IDurableObjectNamespace"] = "DurableObjectNamespace",
        ["Workers.DurableObjectId"] = "DurableObjectId",
        ["Workers.DurableObjectState"] = "DurableObjectState",
        ["Workers.DurableObjectStorage"] = "DurableObjectStorage",
        ["Workers.DurableObjectTransaction"] = "DurableObjectTransaction",
        ["Workers.DurableObjectKvStorage"] = "SyncKvStorage",
        ["Workers.DurableObjectSqlStorage"] = "SqlStorage",
        ["Workers.DurableObjectSqlCursor<T>"] = "SqlStorageCursor",
        ["Workers.DurableObjectContainer"] = "Container",
        ["Workers.ContainerTcpPort"] = "Fetcher",
        ["Workers.ContainerExecProcess"] = "ExecProcess",
        ["Workers.IDynamicDispatcherBinding"] = "DispatchNamespace",
        ["Workers.IRateLimiter"] = "RateLimit",
        ["Workers.IAnalyticsEngineDataset"] = "AnalyticsEngineDataset",
        ["Workers.ISendEmailBinding"] = "SendEmail",
        ["Workers.IAiBinding"] = "Ai",
        ["Workers.IWorkflowBinding"] = "Workflow",
        ["Workers.WorkflowInstance"] = "WorkflowInstance",
        ["Workers.IImagesBinding"] = "ImagesBinding",
        ["Workers.ImagesPipeline"] = "ImageTransformer",
        ["Workers.IMediaBinding"] = "MediaBinding",
        ["Workers.MediaPipeline"] = "MediaTransformer",
        ["Workers.MediaOutput"] = "MediaTransformationResult",
        ["Workers.IVectorizeIndex"] = "Vectorize",
        ["Workers.ISecretStoreBinding"] = "SecretsStoreSecret",
        ["Workers.IHyperdriveBinding"] = "Hyperdrive",
        ["Workers.IVersionMetadataBinding"] = "WorkerVersionMetadata",
        ["Workers.RpcStub"] = "StubBase"
    };

    // Intrinsic kinds whose JavaScript name is a member of the receiver.
    private static readonly HashSet<BindingIntrinsicKind> MemberKinds =
    [
        BindingIntrinsicKind.Direct, BindingIntrinsicKind.Property, BindingIntrinsicKind.Fluent,
        BindingIntrinsicKind.RequestBodyRead,
        BindingIntrinsicKind.KvBytesGet, BindingIntrinsicKind.KvJsonGet, BindingIntrinsicKind.KvJsonPut,
        BindingIntrinsicKind.DurableObjectGet, BindingIntrinsicKind.CacheMatch, BindingIntrinsicKind.DictionaryObject,
        BindingIntrinsicKind.RateLimit, BindingIntrinsicKind.DateResult, BindingIntrinsicKind.IteratorArray,
        BindingIntrinsicKind.QueryNames, BindingIntrinsicKind.CryptoBytesResult,
        BindingIntrinsicKind.CryptoImportKey, BindingIntrinsicKind.CryptoExportJwk
    ];

    // Receivers whose members are not a fixed runtime interface.
    private static readonly HashSet<string> OpenReceivers = ["Workers.RawBinding"];

    // Properties whose runtime name is not the camel-case form of the C# name.
    private static readonly Dictionary<string, string> RenamedProperties = new()
    {
        ["Workers.HtmlElement.NamespaceUri"] = "namespaceURI",
        ["Workers.JsonWebKey.KeyOps"] = "key_ops",
        // Runtime declarations retain the C++ identifier, but the JS dictionary field is public.
        ["Workers.EcdhKeyDeriveParams.Public"] = "$public"
    };

    // Properties the compiler computes instead of reading a runtime member of the same name.
    private static readonly HashSet<string> ComputedProperties =
    [
        "Workers.Request.Path", "Workers.Request.PathAndQuery", "Workers.Request.QueryParameters", "Workers.Request.Url",
        "Workers.Request.Cookies",
        "Workers.QueueMessageBatch<T>.Count", "Workers.QueueMessageBatch<T>.Item",
        "Workers.Headers.Count", "Workers.ScheduledEvent.ScheduledTime"
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
                .Select(property => (type.Name, Property: property.Name)))
            .Where(item => !ComputedProperties.Contains($"{item.Name}.{item.Property}"))
            .Where(item => !HasRuntimeMember(item.Name, RenamedProperties.GetValueOrDefault($"{item.Name}.{item.Property}")
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
    [InlineData("Workers.ContainerExecProcess", "getExitCode", false)]
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
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Workers.slnx")))
            repository = repository.Parent;
        var wrangler = Path.Combine(repository?.FullName ?? "", "node_modules", "wrangler", "bin", "wrangler.js");
        if (!File.Exists(wrangler))
            throw new InvalidOperationException("Run 'npm ci' first: the runtime declarations come from the installed wrangler.");

        var directory = Directory.CreateTempSubdirectory("workers-runtime-types-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "wrangler.jsonc"),
                """{ "name": "runtime-types", "main": "worker.js", "compatibility_date": "2026-10-11", "compatibility_flags": ["webcrypto_modern_algorithms"] }""");
            File.WriteAllText(Path.Combine(directory.FullName, "worker.js"), "export default {};");
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
            return File.ReadAllText(Path.Combine(directory.FullName, "runtime.d.ts")).ReplaceLineEndings("\n");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
