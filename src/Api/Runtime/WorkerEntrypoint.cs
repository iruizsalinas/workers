namespace Workers;

/// <summary>
/// A named Worker entrypoint, exported under its class name and callable over RPC through a service binding.
/// Public methods are RPC methods; override the handlers the entrypoint receives.
/// </summary>
public abstract class WorkerEntrypoint
{
    protected WorkerEntrypoint() { }

    protected Env Env => WorkerApi.NotExecutable<Env>();
    protected ExecutionContext Ctx => WorkerApi.NotExecutable<ExecutionContext>();

    public virtual Task<Response> FetchAsync(Request request) => WorkerApi.NotExecutable<Task<Response>>();
    public virtual Task ScheduledAsync(ScheduledController controller) => WorkerApi.NotExecutable<Task>();
    public virtual Task EmailAsync(ForwardableEmailMessage message) => WorkerApi.NotExecutable<Task>();
    public virtual Task TailAsync(IReadOnlyList<TraceItem> events) => WorkerApi.NotExecutable<Task>();
}

/// <summary>Exports a <see cref="WorkerEntrypoint"/> as the module's default export instead of by its class name.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DefaultExportAttribute : Attribute;
