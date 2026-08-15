namespace Workers;

public interface IWorkflowBinding : IBinding
{
    Task<WorkflowInstance> CreateAsync(WorkflowInstanceCreateOptions? options = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(IEnumerable<WorkflowInstanceCreateOptions> options, CancellationToken cancellationToken = default);
    Task<WorkflowInstance> GetAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class WorkflowInstanceCreateOptions
{
    public string? Id { get; init; }
    public object? Params { get; init; }
}

public sealed class WorkflowInstance
{
    public string Id => WorkerApi.NotExecutable<string>();

    public Task<WorkflowInstanceStatus> StatusAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<WorkflowInstanceStatus>>();
    public Task PauseAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task ResumeAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task TerminateAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task RestartAsync(WorkflowInstanceRestartOptions? options = null, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task SendEventAsync(WorkflowInstanceEventOptions options, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
}

public sealed class WorkflowInstanceStatus
{
    public string Status { get; init; } = "unknown";
    public JsonElement? Output { get; init; }
}

public sealed class WorkflowRetentionOptions
{
    public string? SuccessRetention { get; init; }
    public string? ErrorRetention { get; init; }
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

public sealed class WorkflowInstanceError
{
    public string Name { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class WorkflowRollbackStatus
{
    public string Outcome { get; init; } = "";
    public WorkflowInstanceError? Error { get; init; }
}
