namespace Workers;

/// <summary>
/// The Worker's bindings, read by the name configured in Wrangler. Each accessor returns the binding
/// as its Cloudflare runtime type; <see cref="Get{T}"/> reads any binding or variable.
/// </summary>
public sealed class Env
{
    private Env() { }

    public T Get<T>(string name) => WorkerApi.NotExecutable<T>();
    public bool TryGet<T>(string name, out T value)
    {
        value = default!;
        return WorkerApi.NotExecutable<bool>();
    }

    /// <summary>A text variable.</summary>
    public string Variable(string name) => WorkerApi.NotExecutable<string>();
    /// <summary>A JSON variable, mapped to a C# class or record.</summary>
    public T Variable<T>(string name) => WorkerApi.NotExecutable<T>();
    public string Secret(string name) => WorkerApi.NotExecutable<string>();
    public RawBinding Raw(string name) => WorkerApi.NotExecutable<RawBinding>();

    public KVNamespace KV(string name) => WorkerApi.NotExecutable<KVNamespace>();
    public R2Bucket R2(string name) => WorkerApi.NotExecutable<R2Bucket>();
    public D1Database D1(string name) => WorkerApi.NotExecutable<D1Database>();
    public Queue Queue(string name) => WorkerApi.NotExecutable<Queue>();
    public DurableObjectNamespace DurableObject(string name) => WorkerApi.NotExecutable<DurableObjectNamespace>();
    public Fetcher Service(string name) => WorkerApi.NotExecutable<Fetcher>();
    public Fetcher Assets(string name) => WorkerApi.NotExecutable<Fetcher>();
    public Fetcher MtlsCertificate(string name) => WorkerApi.NotExecutable<Fetcher>();
    public DispatchNamespace DispatchNamespace(string name) => WorkerApi.NotExecutable<DispatchNamespace>();
    public RateLimit RateLimit(string name) => WorkerApi.NotExecutable<RateLimit>();
    public AnalyticsEngineDataset AnalyticsEngine(string name) => WorkerApi.NotExecutable<AnalyticsEngineDataset>();
    public SendEmail SendEmail(string name) => WorkerApi.NotExecutable<SendEmail>();
    public WorkerVersionMetadata VersionMetadata(string name) => WorkerApi.NotExecutable<WorkerVersionMetadata>();
    public Ai Ai(string name) => WorkerApi.NotExecutable<Ai>();
    public Workflow Workflow(string name) => WorkerApi.NotExecutable<Workflow>();
    public ImagesBinding Images(string name) => WorkerApi.NotExecutable<ImagesBinding>();
    public MediaBinding Media(string name) => WorkerApi.NotExecutable<MediaBinding>();
    public Vectorize Vectorize(string name) => WorkerApi.NotExecutable<Vectorize>();
    public SecretsStoreSecret SecretsStoreSecret(string name) => WorkerApi.NotExecutable<SecretsStoreSecret>();
    public Hyperdrive Hyperdrive(string name) => WorkerApi.NotExecutable<Hyperdrive>();
}
