using System.Runtime.InteropServices;
using Xunit;

namespace Workers.Tests;

public sealed class SocketTests
{
    [Fact]
    public void ReadResultCopiesInputBytes()
    {
        var source = new byte[] { 1, 2, 3 };

        var result = SocketReadResult.FromBytes(source);
        source[0] = 9;

        Assert.Equal([1, 2, 3], result.Bytes.ToArray());
    }

    [Fact]
    public void ReadResultBytesReturnsSnapshot()
    {
        var result = SocketReadResult.FromBytes([1, 2, 3]);

        var bytes = result.Bytes;
        Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment));
        segment.Array![segment.Offset] = 9;

        Assert.Equal([1, 2, 3], result.Bytes.ToArray());
    }
}
