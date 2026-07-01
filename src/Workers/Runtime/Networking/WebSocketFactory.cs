using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

internal static partial class WebSocketFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly WebSocketFactoryJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    public static async Task<WebSocketPair> CreatePairAsync(
        string invocationId,
        IBindingDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var invocation = new BindingInvocation(invocationId, "$websocket", "websocket.createPair", "{}");
        var result = await dispatcher.DispatchAsync(invocation, cancellationToken);
        var pair = JsonSerializer.Deserialize(result, JsonContext.WebSocketPairPayload)
            ?? throw new WorkersException("WebSocket pair creation returned an empty result.");

        return new WebSocketPair(
            new WebSocket(invocationId, pair.Client, dispatcher),
            new WebSocket(invocationId, pair.Server, dispatcher));
    }

    public static async Task<WebSocket> ConnectAsync(
        string invocationId,
        string url,
        IEnumerable<string>? protocols,
        IBindingDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var protocolList = ValidateProtocols(protocols);
        var invocation = new BindingInvocation(
            invocationId,
            "$websocket",
            "websocket.connect",
            JsonSerializer.Serialize(
                new WebSocketConnectPayload { Url = url, Protocols = protocolList },
                JsonContext.WebSocketConnectPayload));

        var result = await dispatcher.DispatchAsync(invocation, cancellationToken);
        var connected = JsonSerializer.Deserialize(result, JsonContext.WebSocketHandlePayload)
            ?? throw new WorkersException("WebSocket connect returned an empty result.");

        return new WebSocket(invocationId, connected.Handle, dispatcher);
    }

    private static string[] ValidateProtocols(IEnumerable<string>? protocols)
    {
        if (protocols is null)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<string>();
        foreach (var protocol in protocols)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protocol);
            if (!IsHttpToken(protocol))
                throw new ArgumentException($"'{protocol}' is not a valid WebSocket subprotocol.", nameof(protocols));

            if (!seen.Add(protocol))
                throw new ArgumentException($"Duplicate WebSocket subprotocol '{protocol}'.", nameof(protocols));

            values.Add(protocol);
        }

        return values.ToArray();
    }

    private static bool IsHttpToken(string value)
    {
        foreach (var character in value)
        {
            if (!IsHttpTokenCharacter(character))
                return false;
        }

        return value.Length > 0;
    }

    private static bool IsHttpTokenCharacter(char character) =>
        character is >= 'A' and <= 'Z' ||
        character is >= 'a' and <= 'z' ||
        character is >= '0' and <= '9' ||
        character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';

    private sealed class WebSocketPairPayload
    {
        [JsonPropertyName("client")]
        public string Client { get; set; } = "";

        [JsonPropertyName("server")]
        public string Server { get; set; } = "";
    }

    private sealed class WebSocketConnectPayload
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("protocols")]
        public IReadOnlyList<string> Protocols { get; set; } = [];
    }

    private sealed class WebSocketHandlePayload
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = "";
    }

    [JsonSerializable(typeof(WebSocketPairPayload))]
    [JsonSerializable(typeof(WebSocketConnectPayload))]
    [JsonSerializable(typeof(WebSocketHandlePayload))]
    private sealed partial class WebSocketFactoryJsonContext : JsonSerializerContext
    {
    }
}
