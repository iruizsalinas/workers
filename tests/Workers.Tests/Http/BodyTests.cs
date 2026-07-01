using System.Runtime.InteropServices;
using Xunit;

namespace Workers.Tests;

public sealed class BodyTests
{
    [Fact]
    public void BytesReturnsSnapshot()
    {
        var body = Body.FromBytes([1, 2, 3]);

        var bytes = body.Bytes;
        Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment));
        segment.Array![segment.Offset] = 9;

        Assert.Equal([1, 2, 3], body.Bytes.ToArray());
    }
}
