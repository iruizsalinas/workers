namespace Workers;

public sealed class Workflow
{
    private Workflow() { }

    public Task<WorkflowInstance> GetAsync(string id) => WorkerApi.NotExecutable<Task<WorkflowInstance>>();
    public Task<WorkflowInstance> CreateAsync(WorkflowInstanceCreateOptions? options = null) => WorkerApi.NotExecutable<Task<WorkflowInstance>>();
    /// <summary>Creates up to 100 instances. Duplicate IDs are reported in <see cref="WorkflowBatchCreateResult.Errors"/>
    /// instead of failing the call.</summary>
    public Task<WorkflowBatchCreateResult> CreateBatchAsync(WorkflowBatchCreateOptions options) =>
        WorkerApi.NotExecutable<Task<WorkflowBatchCreateResult>>();
    [Obsolete("Pass a WorkflowBatchCreateOptions with Instances, which reports duplicate IDs instead of omitting them.")]
    public Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(IEnumerable<WorkflowInstanceCreateOptions> batch) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<WorkflowInstance>>>();
    /// <summary>Deletes up to 100 instances and their state. Running instances stop without running rollback handlers.</summary>
    public Task<WorkflowBatchDeleteResult> DeleteBatchAsync(IEnumerable<string> instanceIds) =>
        WorkerApi.NotExecutable<Task<WorkflowBatchDeleteResult>>();
}

public sealed class WorkflowInstanceCreateOptions
{
    /// <summary>A unique instance ID of up to 100 letters, digits, <c>-</c> and <c>_</c>; generated when omitted.</summary>
    public string? Id { get; init; }
    /// <summary>A JSON-serializable value passed to the workflow's <c>run</c> method.</summary>
    public object? Params { get; init; }
    public WorkflowRetention? Retention { get; init; }
    /// <summary>Where to run the instance, such as <c>weur</c>, <c>enam</c> or <c>apac</c>.</summary>
    public string? LocationHint { get; init; }
}

/// <summary>Creates either <see cref="Count"/> instances with generated IDs that share options, or the listed <see cref="Instances"/>.</summary>
public sealed class WorkflowBatchCreateOptions
{
    /// <summary>The number of instances to create, from 1 to 100.</summary>
    public int? Count { get; init; }
    /// <summary>A JSON-serializable value passed to each instance created by <see cref="Count"/>.</summary>
    public object? Params { get; init; }
    public WorkflowRetention? Retention { get; init; }
    public string? LocationHint { get; init; }
    /// <summary>Between 1 and 100 instances to create. Cannot be combined with <see cref="Count"/>.</summary>
    public IReadOnlyList<WorkflowInstanceCreateOptions>? Instances { get; init; }
}

/// <summary>How long a finished instance's state is kept, as a duration such as <c>"1 day"</c> or <c>"7 days"</c>.</summary>
public sealed class WorkflowRetention
{
    /// <summary>Retention after the instance completes or is terminated.</summary>
    public string? SuccessRetention { get; init; }
    /// <summary>Retention after the instance errors.</summary>
    public string? ErrorRetention { get; init; }
}

public sealed class WorkflowBatchCreateResult
{
    private WorkflowBatchCreateResult() { }

    /// <summary>The created instances, in input order.</summary>
    public IReadOnlyList<WorkflowInstance> Created => WorkerApi.NotExecutable<IReadOnlyList<WorkflowInstance>>();
    public IReadOnlyList<WorkflowBatchCreateError> Errors => WorkerApi.NotExecutable<IReadOnlyList<WorkflowBatchCreateError>>();
}

public sealed class WorkflowBatchCreateError
{
    private WorkflowBatchCreateError() { }

    /// <summary>The position of the failed entry in the batch.</summary>
    public int Index => WorkerApi.NotExecutable<int>();
    public string? Id => WorkerApi.NotExecutable<string?>();
    /// <summary>10405 when the ID already exists, or 10415 when an earlier entry in the batch uses it.</summary>
    public int Code => WorkerApi.NotExecutable<int>();
    public string Message => WorkerApi.NotExecutable<string>();
}

public sealed class WorkflowBatchDeleteResult
{
    private WorkflowBatchDeleteResult() { }

    public IReadOnlyList<WorkflowDeletedInstance> Deleted => WorkerApi.NotExecutable<IReadOnlyList<WorkflowDeletedInstance>>();
    public IReadOnlyList<WorkflowBatchDeleteError> Errors => WorkerApi.NotExecutable<IReadOnlyList<WorkflowBatchDeleteError>>();
}

public sealed class WorkflowDeletedInstance
{
    private WorkflowDeletedInstance() { }

    public string Id => WorkerApi.NotExecutable<string>();
}

public sealed class WorkflowBatchDeleteError
{
    private WorkflowBatchDeleteError() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public int Code => WorkerApi.NotExecutable<int>();
    public string Message => WorkerApi.NotExecutable<string>();
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
    /// <summary>Deletes the instance and its state. A running instance stops without running rollback handlers.</summary>
    public Task DeleteAsync() => WorkerApi.NotExecutable<Task>();
    public Task SendEventAsync(WorkflowInstanceSendEventOptions @event) => WorkerApi.NotExecutable<Task>();
    /// <summary>Streams the instance's lifecycle events until it completes, errors or is terminated.</summary>
    public Task<WorkflowInstanceSubscription> SubscribeAsync(WorkflowInstanceSubscribeOptions? options = null) =>
        WorkerApi.NotExecutable<Task<WorkflowInstanceSubscription>>();
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

/// <summary>An event delivered to an instance waiting in <c>step.waitForEvent</c>.</summary>
public sealed class WorkflowInstanceSendEventOptions
{
    public required string Type { get; init; }
    public object? Payload { get; init; }
}

public sealed class WorkflowInstanceSubscribeOptions
{
    /// <summary>Starts after the event with this <see cref="WorkflowInstanceEvent.EventId"/>, to resume a subscription.</summary>
    public int? Cursor { get; init; }
    /// <summary>The event types to receive, such as <c>step_completed</c> or <c>workflow_errored</c>; all types when omitted.</summary>
    public IReadOnlyList<string>? Filter { get; init; }
}

/// <summary>A stream of an instance's events. Dispose it to stop receiving events.</summary>
public sealed class WorkflowInstanceSubscription : IDisposable
{
    private WorkflowInstanceSubscription() { }

    /// <summary>Waits for the next event, or returns null when the instance has finished.</summary>
    public Task<WorkflowInstanceEvent?> NextAsync() => WorkerApi.NotExecutable<Task<WorkflowInstanceEvent?>>();
    public void Dispose() => WorkerApi.NotExecutable();
}

/// <summary>
/// A lifecycle event of a workflow instance. <see cref="Type"/> names the event, such as <c>workflow_started</c>,
/// <c>step_completed</c>, <c>attempt_errored</c> or <c>workflow_completed</c>; fields an event type does not carry are null.
/// </summary>
public sealed class WorkflowInstanceEvent
{
    private WorkflowInstanceEvent() { }

    public string Type => WorkerApi.NotExecutable<string>();
    public string InstanceId => WorkerApi.NotExecutable<string>();
    /// <summary>The event's position in the instance's history; pass it as <see cref="WorkflowInstanceSubscribeOptions.Cursor"/> to resume.</summary>
    public int EventId => WorkerApi.NotExecutable<int>();
    public Date Timestamp => WorkerApi.NotExecutable<Date>();
    public string? StepName => WorkerApi.NotExecutable<string?>();
    /// <summary>The attempt number of an <c>attempt_*</c> or <c>rollback_attempt_*</c> event.</summary>
    public int? Attempt => WorkerApi.NotExecutable<int?>();
    /// <summary>The instance params of a <c>workflow_started</c> event.</summary>
    public JsonValue? Params => WorkerApi.NotExecutable<JsonValue?>();
    /// <summary>The result of a <c>workflow_completed</c> or <c>step_completed</c> event.</summary>
    public JsonValue? Output => WorkerApi.NotExecutable<JsonValue?>();
    public InstanceStatusError? Error => WorkerApi.NotExecutable<InstanceStatusError?>();
    public double? RetryDelayMs => WorkerApi.NotExecutable<double?>();
    /// <summary>The duration of a <c>sleep_started</c> event.</summary>
    public double? DurationMs => WorkerApi.NotExecutable<double?>();
    /// <summary>The awaited event type of a <c>wait_started</c> event.</summary>
    public string? EventType => WorkerApi.NotExecutable<string?>();
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
