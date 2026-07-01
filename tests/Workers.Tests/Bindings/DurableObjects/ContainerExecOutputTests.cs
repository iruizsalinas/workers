using Xunit;

namespace Workers.Tests;

public sealed class ContainerExecOutputTests
{
    [Fact]
    public void InitCopiesOutputBytes()
    {
        var stdout = new byte[] { 1, 2, 3 };
        var stderr = new byte[] { 4, 5, 6 };

        var output = new ContainerExecOutput
        {
            Stdout = stdout,
            Stderr = stderr,
            ExitCode = 7
        };
        stdout[0] = 9;
        stderr[0] = 9;

        Assert.Equal([1, 2, 3], output.Stdout);
        Assert.Equal([4, 5, 6], output.Stderr);
        Assert.Equal(7, output.ExitCode);
    }

    [Fact]
    public void OutputPropertiesReturnSnapshots()
    {
        var output = new ContainerExecOutput
        {
            Stdout = [1, 2, 3],
            Stderr = [4, 5, 6]
        };

        var stdout = output.Stdout;
        var stderr = output.Stderr;
        stdout[0] = 9;
        stderr[0] = 9;

        Assert.Equal([1, 2, 3], output.Stdout);
        Assert.Equal([4, 5, 6], output.Stderr);
    }
}
