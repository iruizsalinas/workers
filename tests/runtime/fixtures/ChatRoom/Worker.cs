using Workers;

namespace ChatRoomScenario;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        var parts = new Url(request.Url).Pathname.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0] != "rooms")
            return Task.FromResult(new Response("Use /rooms/<room>") { Status = 404 });

        return environment.DurableObject("ROOMS")
            .GetByName(parts[1])
            .FetchAsync(request);
    }
}

public sealed class ChatRoom : DurableObject
{
    public ChatRoom() => Ctx.SetWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));

    public override Task<Response> FetchAsync(Request request)
    {
        var upgrade = request.Headers.Get("upgrade");
        if (upgrade is null || upgrade.ToLowerInvariant() != "websocket")
            return Task.FromResult(new Response("Expected WebSocket") { Status = 426 });

        var name = new Url(request.Url).SearchParams.Get("name") ?? "anonymous";
        var pair = new WebSocketPair();
        var client = pair.Client;
        var server = pair.Server;
        server.SerializeAttachment(new ChatAttachment(name, new Date().ToIsoString()));
        Ctx.AcceptWebSocket(server, [$"user:{name}"]);
        server.Send(Json.Stringify(new { type = "welcome", name, online = Ctx.GetWebSockets().Count }));
        Broadcast(new { type = "join", name }, server);
        return Task.FromResult(new Response { Status = 101, WebSocket = client });
    }

    public override Task WebSocketMessageAsync(WebSocket socket, WebSocketMessage message)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>()
            ?? new ChatAttachment("anonymous", new Date().ToIsoString());
        var text = message.AsText();
        if (text == "attachment-graph")
        {
            var restored = false;
            string? error = null;
            try
            {
                var graph = new AttachmentGraph();
                graph.Next = graph;
                socket.SerializeAttachment(graph);
                var value = socket.DeserializeAttachment<AttachmentGraph>();
                restored = value is not null && value.Next == value && value.Describe() == "self";
            }
            catch (Exception exception) { error = exception.Message; }
            finally { socket.SerializeAttachment(attachment); }
            socket.Send(Json.Stringify(new { type = "attachment-graph", restored, error }));
            return Task.CompletedTask;
        }
        if (text == "who")
        {
            var users = new List<string>();
            foreach (var peer in Ctx.GetWebSockets())
            {
                var info = peer.DeserializeAttachment<ChatAttachment>();
                if (info is not null)
                    users.Add(info.Name);
            }
            socket.Send(Json.Stringify(new { type = "users", users }));
            return Task.CompletedTask;
        }

        Broadcast(new
        {
            type = "message",
            from = attachment.Name,
            text,
            at = new Date().ToIsoString()
        });
        return Task.CompletedTask;
    }

    public override Task WebSocketCloseAsync(WebSocket socket, int code, string reason, bool wasClean)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>();
        if (attachment is not null)
            Broadcast(new { type = "leave", name = attachment.Name, code, reason, wasClean }, socket);
        return Task.CompletedTask;
    }

    public override Task WebSocketErrorAsync(WebSocket socket, JsonValue error)
    {
        var attachment = socket.DeserializeAttachment<ChatAttachment>();
        Log.Error($"WebSocket error for {attachment?.Name ?? "anonymous"}");
        Log.Error(error);
        return Task.CompletedTask;
    }

    private void Broadcast<T>(T payload, WebSocket? except = null)
    {
        foreach (var socket in Ctx.GetWebSockets())
            if (socket != except)
                socket.Send(Json.Stringify(payload));
    }
}

public sealed record ChatAttachment(string Name, string JoinedAt);

public sealed class AttachmentGraph
{
    public AttachmentGraph? Next { get; set; }
    public Dictionary<int, int> Values { get; init; } = new() { [1] = 2 };
    public string Describe() => Next == this && Values[1] == 2 ? "self" : "other";
}
