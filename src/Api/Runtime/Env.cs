namespace Workers;

public sealed class Env
{
    public T Get<T>(string name) => WorkerApi.NotExecutable<T>();
    public bool TryGet<T>(string name, out T value)
    {
        value = default!;
        return WorkerApi.NotExecutable<bool>();
    }

    public string Variable(string name) => WorkerApi.NotExecutable<string>();
    public T Variable<T>(string name) => WorkerApi.NotExecutable<T>();
    public string Secret(string name) => WorkerApi.NotExecutable<string>();
    public RawBinding Raw(string name) => WorkerApi.NotExecutable<RawBinding>();
    public IKvNamespace Kv(string name) => WorkerApi.NotExecutable<IKvNamespace>();
    public IR2Bucket R2(string name) => WorkerApi.NotExecutable<IR2Bucket>();
    public IServiceBinding Service(string name) => WorkerApi.NotExecutable<IServiceBinding>();
    public IFetcherBinding Assets(string name) => WorkerApi.NotExecutable<IFetcherBinding>();
    public IFetcherBinding Mtls(string name) => WorkerApi.NotExecutable<IFetcherBinding>();
    public IDynamicDispatcherBinding Dispatcher(string name) => WorkerApi.NotExecutable<IDynamicDispatcherBinding>();
    public IQueueProducer Queue(string name) => WorkerApi.NotExecutable<IQueueProducer>();
    public ID1Database D1(string name) => WorkerApi.NotExecutable<ID1Database>();
    public IDurableObjectNamespace DurableObject(string name) => WorkerApi.NotExecutable<IDurableObjectNamespace>();
    public IRateLimiter RateLimiter(string name) => WorkerApi.NotExecutable<IRateLimiter>();
    public IAnalyticsEngineDataset Analytics(string name) => WorkerApi.NotExecutable<IAnalyticsEngineDataset>();
    public ISendEmailBinding Email(string name) => WorkerApi.NotExecutable<ISendEmailBinding>();
    public IVersionMetadataBinding Version(string name) => WorkerApi.NotExecutable<IVersionMetadataBinding>();
    public IAiBinding Ai(string name) => WorkerApi.NotExecutable<IAiBinding>();
    public IWorkflowBinding Workflow(string name) => WorkerApi.NotExecutable<IWorkflowBinding>();
    public IImagesBinding Images(string name) => WorkerApi.NotExecutable<IImagesBinding>();
    public IMediaBinding Media(string name) => WorkerApi.NotExecutable<IMediaBinding>();
    public IVectorizeIndex Vectorize(string name) => WorkerApi.NotExecutable<IVectorizeIndex>();
    public ISecretStoreBinding SecretStore(string name) => WorkerApi.NotExecutable<ISecretStoreBinding>();
    public IHyperdriveBinding Hyperdrive(string name) => WorkerApi.NotExecutable<IHyperdriveBinding>();
}
