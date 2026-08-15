using Workers;

namespace ChatRoomScenario;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var parts = request.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0] != "rooms")
            return Task.FromResult(Response.Text("Use /rooms/<room>", 404));

        return environment.DurableObject("ROOMS")
            .GetByName(parts[1])
            .FetchAsync(request);
    }
}

[DurableObject("ChatRoom")]
public sealed class ChatRoom
{
    private readonly DurableObjectState _state;

    public ChatRoom(DurableObjectState state, Env environment)
    {
        _state = state;
        _state.SetWebSocketAutoResponse(new WebSocketAutoResponse("ping", "pong"));
    }

    public Response FetchAsync(Request request)
    {
        var upgrade = request.Headers.Get("upgrade");
        if (upgrade is null || upgrade.ToLowerInvariant() != "websocket")
            return Response.Text("Expected WebSocket", 426);

        var name = request.QueryParameters.Get("name") ?? "anonymous";
        var pair = WebSocketPair.Create();
        var client = pair.Client;
        var server = pair.Server;
        server.SerializeAttachment(new ChatAttachment(name, DateTimeOffset.UtcNow.ToString("O")));
        _state.AcceptWebSocket(server, [$"user:{name}"]);
        server.SendJson(new { type = "welcome", name, online = _state.GetWebSockets().Count });
        Broadcast(new { type = "join", name }, server);
        return Response.WebSocket(client);
    }

    public void WebSocketMessageAsync(WebSocket socket, WebSocketMessage message)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>()
            ?? new ChatAttachment("anonymous", DateTimeOffset.UtcNow.ToString("O"));
        var text = message.AsText();
        if (text == "who")
        {
            var users = new List<string>();
            foreach (var peer in _state.GetWebSockets())
            {
                var info = peer.DeserializeAttachment<ChatAttachment>();
                if (info is not null)
                    users.Add(info.Name);
            }
            socket.SendJson(new { type = "users", users });
            return;
        }

        Broadcast(new
        {
            type = "message",
            from = attachment.Name,
            text,
            at = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public void WebSocketCloseAsync(WebSocket socket, int code, string reason, bool wasClean)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>();
        if (attachment is not null)
            Broadcast(new { type = "leave", name = attachment.Name, code, reason, wasClean }, socket);
    }

    public void WebSocketErrorAsync(WebSocket socket, WebSocketError error)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>();
        Console.Error.WriteLine($"WebSocket error for {attachment?.Name ?? "anonymous"}: {error.Message}");
    }

    private void Broadcast<T>(T payload, WebSocket? except = null)
    {
        foreach (var socket in _state.GetWebSockets())
            if (socket != except)
                socket.SendJson(payload);
    }
}

public sealed record ChatAttachment(string Name, string JoinedAt);
