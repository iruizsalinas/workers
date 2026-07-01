using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Writes messages to the Workers runtime console.</summary>
public sealed partial class Log
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly LogJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal Log(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Writes a log message.</summary>
    public Task LogAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("log", message, cancellationToken);

    /// <summary>Writes a debug message.</summary>
    public Task DebugAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("debug", message, cancellationToken);

    /// <summary>Writes a warning message.</summary>
    public Task WarnAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("warn", message, cancellationToken);

    /// <summary>Writes an error message.</summary>
    public Task ErrorAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("error", message, cancellationToken);

    private Task WriteAsync(string level, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var invocation = new BindingInvocation(
            _invocationId,
            "$runtime",
            "runtime.console",
            JsonSerializer.Serialize(
                new ConsoleMessage { Level = level, Message = message },
                JsonContext.ConsoleMessage));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed class ConsoleMessage
    {
        [JsonPropertyName("level")]
        public string Level { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    [JsonSerializable(typeof(ConsoleMessage))]
    private sealed partial class LogJsonContext : JsonSerializerContext
    {
    }
}
