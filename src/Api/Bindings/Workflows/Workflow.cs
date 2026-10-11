namespace Workers;

public sealed class Workflow
{
    private Workflow() { }

    public Task<WorkflowInstance> GetAsync(string id) => WorkerApi.NotExecutable<Task<WorkflowInstance>>();
    public Task<WorkflowInstance> CreateAsync(WorkflowInstanceCreateOptions? options = null) => WorkerApi.NotExecutable<Task<WorkflowInstance>>();
    public Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(IEnumerable<WorkflowInstanceCreateOptions> batch) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<WorkflowInstance>>>();
}

public sealed class WorkflowInstanceCreateOptions
{
    /// <summary>A unique instance ID; generated when omitted.</summary>
    public string? Id { get; init; }
    /// <summary>A JSON-serializable value passed to the workflow's <c>run</c> method.</summary>
    public object? Params { get; init; }
}

public sealed class WorkflowInstance
{
    private WorkflowInstance() { }

    public string Id => WorkerApi.NotExecutable<string>();

    public Task<InstanceStatus> StatusAsync() => WorkerApi.NotExecutable<Task<InstanceStatus>>();
    public Task PauseAsync() => WorkerApi.NotExecutable<Task>();
    public Task ResumeAsync() => WorkerApi.NotExecutable<Task>();
    public Task TerminateAsync(WorkflowInstanceTerminateOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task RestartAsync(WorkflowInstanceRestartOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task SendEventAsync(WorkflowInstanceEvent @event) => WorkerApi.NotExecutable<Task>();
}

public sealed class WorkflowInstanceTerminateOptions
{
    /// <summary>Runs the rollback handlers of completed steps before terminating.</summary>
    public bool? Rollback { get; init; }
}

public sealed class WorkflowInstanceRestartOptions
{
    /// <summary>The step to restart from; the whole instance restarts when omitted.</summary>
    public WorkflowRestartStep? From { get; init; }
}

public sealed class WorkflowRestartStep
{
    public required string Name { get; init; }
    /// <summary>Which occurrence of a step with this name, starting at 1.</summary>
    public int? Count { get; init; }
    /// <summary><c>do</c>, <c>sleep</c> or <c>waitForEvent</c>.</summary>
    public string? Type { get; init; }
}

public sealed class WorkflowInstanceEvent
{
    public required string Type { get; init; }
    public object? Payload { get; init; }
}

public sealed class InstanceStatus
{
    private InstanceStatus() { }

    /// <summary>
    /// <c>queued</c>, <c>running</c>, <c>paused</c>, <c>errored</c>, <c>terminated</c>, <c>complete</c>, <c>waiting</c>,
    /// <c>waitingForPause</c>, <c>rollingBack</c> or <c>unknown</c>.
    /// </summary>
    public string Status => WorkerApi.NotExecutable<string>();
    public InstanceStatusError? Error => WorkerApi.NotExecutable<InstanceStatusError?>();
    public JsonValue? Output => WorkerApi.NotExecutable<JsonValue?>();
}

public sealed class InstanceStatusError
{
    private InstanceStatusError() { }

    public string Name => WorkerApi.NotExecutable<string>();
    public string Message => WorkerApi.NotExecutable<string>();
}
