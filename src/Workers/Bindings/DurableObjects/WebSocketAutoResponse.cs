namespace Workers;

/// <summary>An automatic hibernatable WebSocket request/response pair.</summary>
public sealed record WebSocketAutoResponse(string Request, string Response);
