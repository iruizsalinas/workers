namespace Workers;

public interface IWorkflowBinding : IBinding
{
    Task<WorkflowInstance> CreateAsync(WorkflowInstanceCreateOptions? options = null);
    Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(IEnumerable<WorkflowInstanceCreateOptions> options);
    Task<WorkflowInstance> GetAsync(string id);
}

public sealed class WorkflowInstanceCreateOptions
{
    public string? Id { get; init; }
    public object? Params { get; init; }
}

public sealed class WorkflowInstance
{
    public string Id => WorkerApi.NotExecutable<string>();

    public Task<WorkflowInstanceStatus> StatusAsync() => WorkerApi.NotExecutable<Task<WorkflowInstanceStatus>>();
    public Task PauseAsync() => WorkerApi.NotExecutable<Task>();
    public Task ResumeAsync() => WorkerApi.NotExecutable<Task>();
    public Task TerminateAsync() => WorkerApi.NotExecutable<Task>();
    public Task RestartAsync(WorkflowInstanceRestartOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task SendEventAsync(WorkflowInstanceEventOptions options) => WorkerApi.NotExecutable<Task>();
}

public sealed class WorkflowInstanceStatus
{
    public string Status { get; init; } = "unknown";
    public JsonValue? Output { get; init; }
}

public sealed class WorkflowInstanceRestartOptions
{
    public WorkflowRestartFromStep? From { get; init; }
}

public sealed class WorkflowRestartFromStep
{
    public required string Name { get; init; }
    public int? Count { get; init; }
    public string? Type { get; init; }
}

public sealed class WorkflowInstanceEventOptions
{
    public required string Type { get; init; }
    public object? Payload { get; init; }
}
