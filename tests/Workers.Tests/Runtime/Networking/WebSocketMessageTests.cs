using System.Runtime.InteropServices;
using Xunit;

namespace Workers.Tests;

public sealed class WebSocketMessageTests
{
    [Fact]
    public void MessageCopiesInputBytes()
    {
        var source = new byte[] { 1, 2, 3 };

        var message = new WebSocketMessage(text: null, source);
        source[0] = 9;

        Assert.Equal([1, 2, 3], message.Bytes.ToArray());
    }

    [Fact]
    public void MessageBytesReturnsSnapshot()
    {
        var message = new WebSocketMessage(text: null, [1, 2, 3]);

        var bytes = message.Bytes;
        Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment));
        segment.Array![segment.Offset] = 9;

        Assert.Equal([1, 2, 3], message.Bytes.ToArray());
    }

    [Fact]
    public void EventBytesReturnsSnapshot()
    {
        var bodyBase64 = Convert.ToBase64String([1, 2, 3]);
        var message = WebSocketEvent.FromEnvelope(
            new WebSocket.WebSocketEventEnvelope(
                "message",
                Text: null,
                bodyBase64,
                Code: null,
                Reason: null,
                WasClean: null));

        var bytes = message.Bytes;
        Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment));
        segment.Array![segment.Offset] = 9;

        Assert.Equal([1, 2, 3], message.Bytes.ToArray());
    }
}
