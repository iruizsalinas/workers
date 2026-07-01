namespace Workers;

/// <summary>Options used when starting a Durable Object container.</summary>
public sealed record ContainerStartOptions
{
    /// <summary>Environment variables passed to the container process.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>Entrypoint command and arguments for the container process.</summary>
    public IReadOnlyList<string>? Entrypoint { get; init; }

    /// <summary>Whether the container can access the public Internet.</summary>
    public bool? EnableInternet { get; init; }
}

/// <summary>Options used when executing a process inside a running Durable Object container.</summary>
public sealed record ContainerExecOptions
{
    /// <summary>Process working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>Environment variable additions and overrides for this process.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>User to run the process as.</summary>
    public string? User { get; init; }

    /// <summary>Standard input mode, such as "pipe".</summary>
    public string? Stdin { get; init; }

    /// <summary>Standard output mode, such as "pipe" or "ignore".</summary>
    public string? Stdout { get; init; }

    /// <summary>Standard error mode, such as "pipe", "ignore", or "combined".</summary>
    public string? Stderr { get; init; }
}

/// <summary>Buffered output from a Durable Object container process.</summary>
public sealed class ContainerExecOutput
{
    private byte[] _stdout = [];
    private byte[] _stderr = [];

    /// <summary>Captured standard output bytes.</summary>
    public byte[] Stdout
    {
        get => _stdout.ToArray();
        init => _stdout = Copy(value);
    }

    /// <summary>Captured standard error bytes.</summary>
    public byte[] Stderr
    {
        get => _stderr.ToArray();
        init => _stderr = Copy(value);
    }

    /// <summary>The process exit code.</summary>
    public int ExitCode { get; init; }

    private static byte[] Copy(byte[]? value) => value is null ? [] : value.ToArray();
}
