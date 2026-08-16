namespace Workers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class WorkerEntrypointAttribute(string? exportName = null) : Attribute
{
    public string? ExportName { get; } = exportName;
}

public abstract class WorkerEntrypoint
{
    protected Env Environment => WorkerApi.NotExecutable<Env>();
    protected Context Context => WorkerApi.NotExecutable<Context>();
}
