using System.Text.Json;

namespace Workers;

/// <summary>Request context for scheduling background work tied to the current event.</summary>
public sealed class Context
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<Task> _tasks = [];
    private readonly JsonElement? _props;

    /// <summary>Creates an empty execution context.</summary>
    public Context()
    {
    }

    internal Context(JsonElement? props)
    {
        _props = props;
    }

    /// <summary>Tasks scheduled to continue after the response is produced.</summary>
    public IReadOnlyList<Task> PendingTasks => _tasks.AsReadOnly();

    /// <summary>True when the handler requested pass-through behavior for unhandled exceptions.</summary>
    internal bool PassThroughOnExceptionRequested { get; private set; }

    /// <summary>Schedules work using the Workers waitUntil model.</summary>
    public void WaitUntil(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _tasks.Add(task);
    }

    /// <summary>Requests Workers fail-open pass-through behavior if the script throws an unhandled exception.</summary>
    public void PassThroughOnException()
    {
        PassThroughOnExceptionRequested = true;
    }

    /// <summary>Deserializes props supplied to the Worker execution context.</summary>
    public T Props<T>(JsonSerializerOptions? options = null)
    {
        options ??= DefaultJsonOptions;

        var props = _props;
        if (props is null || props.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return JsonSerializer.Deserialize<T>("{}", options) ?? throw new WorkersException("Execution context props could not be deserialized.");

        return props.Value.Deserialize<T>(options)
            ?? throw new WorkersException("Execution context props could not be deserialized.");
    }
}
