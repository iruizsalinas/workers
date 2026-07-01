using System.Text.Json;

namespace Workers;

/// <summary>Options for creating a Workflow instance.</summary>
public sealed class WorkflowInstanceCreateOptions
{
    /// <summary>An optional user-provided Workflow instance ID.</summary>
    public string? Id { get; init; }

    /// <summary>JSON-serializable parameters passed to the Workflow instance.</summary>
    public object? Params { get; init; }

    /// <summary>Optional retention settings for the created Workflow instance.</summary>
    public WorkflowRetentionOptions? Retention { get; init; }
}

/// <summary>Retention settings for a Workflow instance.</summary>
public sealed class WorkflowRetentionOptions
{
    /// <summary>How long to retain state after successful completion.</summary>
    public string? SuccessRetention { get; init; }

    /// <summary>How long to retain state after an errored or terminated completion.</summary>
    public string? ErrorRetention { get; init; }
}

/// <summary>Options for restarting a Workflow instance.</summary>
public sealed class WorkflowInstanceRestartOptions
{
    /// <summary>The step to restart from. When omitted, the instance restarts from the beginning.</summary>
    public WorkflowRestartFromStep? From { get; init; }
}

/// <summary>Identifies the Workflow step to restart from.</summary>
public sealed class WorkflowRestartFromStep
{
    /// <summary>The step name.</summary>
    public required string Name { get; init; }

    /// <summary>The 1-based occurrence count for steps sharing the same name.</summary>
    public int? Count { get; init; }

    /// <summary>The step type, such as <c>do</c>, <c>sleep</c>, or <c>waitForEvent</c>.</summary>
    public string? Type { get; init; }
}

/// <summary>Options for sending an event to a waiting Workflow instance.</summary>
public sealed class WorkflowInstanceEventOptions
{
    /// <summary>The event type expected by a matching Workflow waitForEvent step.</summary>
    public required string Type { get; init; }

    /// <summary>JSON-serializable event payload.</summary>
    public object? Payload { get; init; }
}

/// <summary>Current status details for a Workflow instance.</summary>
public sealed class WorkflowInstanceStatus
{
    /// <summary>The Workflow instance status string supplied by Cloudflare.</summary>
    public string Status { get; init; } = "unknown";

    /// <summary>Error details when the instance is errored.</summary>
    public WorkflowInstanceError? Error { get; init; }

    /// <summary>The JSON-compatible output returned by the Workflow, when available.</summary>
    public JsonElement? Output { get; init; }

    /// <summary>Rollback status, when a rollback has run or is running.</summary>
    public WorkflowRollbackStatus? Rollback { get; init; }
}

/// <summary>Error details supplied for a Workflow instance.</summary>
public sealed class WorkflowInstanceError
{
    /// <summary>The error name.</summary>
    public string Name { get; init; } = "";

    /// <summary>The error message.</summary>
    public string Message { get; init; } = "";
}

/// <summary>Rollback status details supplied for a Workflow instance.</summary>
public sealed class WorkflowRollbackStatus
{
    /// <summary>The rollback outcome string supplied by Cloudflare.</summary>
    public string Outcome { get; init; } = "";

    /// <summary>Rollback error details, when rollback failed.</summary>
    public WorkflowInstanceError? Error { get; init; }
}

/// <summary>A handle for a specific Workflow instance.</summary>
public sealed class WorkflowInstance
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    internal WorkflowInstance(
        string invocationId,
        string bindingName,
        string id,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        WorkflowIdentifierValidation.Validate(id, nameof(id), "Workflow instance IDs");
        _invocationId = invocationId;
        _bindingName = bindingName;
        Id = id;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The Workflow instance ID.</summary>
    public string Id { get; }

    /// <summary>Gets the current Workflow instance status.</summary>
    public async Task<WorkflowInstanceStatus> StatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("workflow.instance.status", new WorkflowInstanceIdRequest(Id), cancellationToken)
            ;
        return JsonSerializer.Deserialize<WorkflowInstanceStatus>(result, JsonOptions)
            ?? throw new WorkersException("Workflow instance status returned an empty result.");
    }

    /// <summary>Pauses the Workflow instance.</summary>
    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("workflow.instance.pause", new WorkflowInstanceIdRequest(Id), cancellationToken);

    /// <summary>Resumes the Workflow instance.</summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("workflow.instance.resume", new WorkflowInstanceIdRequest(Id), cancellationToken);

    /// <summary>Terminates the Workflow instance.</summary>
    public Task TerminateAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("workflow.instance.terminate", new WorkflowInstanceIdRequest(Id), cancellationToken);

    /// <summary>Restarts the Workflow instance.</summary>
    public Task RestartAsync(WorkflowInstanceRestartOptions? options = null, CancellationToken cancellationToken = default) =>
        DispatchAsync("workflow.instance.restart", new WorkflowInstanceRestartRequest(Id, options), cancellationToken);

    /// <summary>Sends an event to a running Workflow instance.</summary>
    public Task SendEventAsync(WorkflowInstanceEventOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        WorkflowIdentifierValidation.Validate(options.Type, nameof(options), "Workflow event types");
        return DispatchAsync("workflow.instance.sendEvent", new WorkflowInstanceEventRequest(Id, options), cancellationToken);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }
}

internal sealed class WorkflowBinding : IWorkflowBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public WorkflowBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<WorkflowInstance> CreateAsync(CancellationToken cancellationToken = default) =>
        CreateAsync(options: null, cancellationToken);

    public async Task<WorkflowInstance> CreateAsync(
        WorkflowInstanceCreateOptions? options,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateOptions(options, requireId: false);
        var result = await DispatchAsync("workflow.create", new WorkflowCreateRequest(options), cancellationToken)
            ;
        return ToInstance(result);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> CreateBatchAsync(
        IEnumerable<WorkflowInstanceCreateOptions> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var items = batch.ToArray();
        if (items.Length is 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(batch), items.Length, "Workflow createBatch supports from 1 through 100 instances.");

        foreach (var item in items)
            ValidateCreateOptions(item, requireId: true);

        var result = await DispatchAsync("workflow.createBatch", new WorkflowCreateBatchRequest(items), cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<WorkflowInstancesEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Workflow createBatch returned an empty result.");

        return envelope.Instances.Select(ToInstance).ToArray();
    }

    public async Task<WorkflowInstance> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        WorkflowIdentifierValidation.Validate(id, nameof(id), "Workflow instance IDs");
        var result = await DispatchAsync("workflow.get", new WorkflowInstanceIdRequest(id), cancellationToken)
            ;
        return ToInstance(result);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private WorkflowInstance ToInstance(string result)
    {
        var envelope = JsonSerializer.Deserialize<WorkflowInstanceEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Workflow operation returned an empty instance result.");
        return ToInstance(envelope);
    }

    private WorkflowInstance ToInstance(WorkflowInstanceEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Id))
            throw new WorkersException("Workflow instance result did not include an id.");

        return new WorkflowInstance(_invocationId, _bindingName, envelope.Id, _dispatcher);
    }

    private static void ValidateCreateOptions(WorkflowInstanceCreateOptions? options, bool requireId)
    {
        if (options is null)
        {
            if (requireId)
                throw new ArgumentException("Workflow createBatch entries must include an id.");

            return;
        }

        if (requireId && string.IsNullOrWhiteSpace(options.Id))
            throw new ArgumentException("Workflow createBatch entries must include an id.", nameof(options));

        if (options.Id is not null)
            WorkflowIdentifierValidation.Validate(options.Id, nameof(options), "Workflow instance IDs");
    }
}

internal static class WorkflowIdentifierValidation
{
    public static void Validate(string value, string paramName, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        if (value.Length > 100)
            throw new ArgumentOutOfRangeException(paramName, value.Length, $"{label} must be 100 characters or fewer.");

        if (!IsIdentifierStart(value[0]) || value.Skip(1).Any(static c => !IsIdentifierPart(c)))
            throw new ArgumentException($"{label} must match ^[a-zA-Z0-9_][a-zA-Z0-9-_]*$.", paramName);
    }

    private static bool IsIdentifierStart(char value) =>
        value is '_' || char.IsAsciiLetterOrDigit(value);

    private static bool IsIdentifierPart(char value) =>
        IsIdentifierStart(value) || value is '-';
}

internal sealed record WorkflowCreateRequest(WorkflowInstanceCreateOptions? Options);

internal sealed record WorkflowCreateBatchRequest(IReadOnlyList<WorkflowInstanceCreateOptions> Batch);

internal sealed record WorkflowInstanceIdRequest(string Id);

internal sealed record WorkflowInstanceRestartRequest(string Id, WorkflowInstanceRestartOptions? Options);

internal sealed record WorkflowInstanceEventRequest(string Id, WorkflowInstanceEventOptions Options);

internal sealed record WorkflowInstanceEnvelope(string Id);

internal sealed record WorkflowInstancesEnvelope(IReadOnlyList<WorkflowInstanceEnvelope> Instances);
