namespace Workers.Build;

/// <summary>Controls which Worker runtime adapter entrypoints are emitted.</summary>
internal sealed record RuntimeAdapterOptions
{
    /// <summary>Emits every runtime adapter entrypoint.</summary>
    public static RuntimeAdapterOptions All { get; } = new()
    {
        IncludeFetch = true,
        IncludeScheduled = true,
        IncludeQueue = true,
        IncludeEmail = true,
        IncludeTail = true,
        IncludeDurableObjects = true
    };

    /// <summary>Whether the adapter should expose a fetch event entrypoint.</summary>
    public bool IncludeFetch { get; init; }

    /// <summary>Whether the adapter should expose a scheduled event entrypoint.</summary>
    public bool IncludeScheduled { get; init; }

    /// <summary>Whether the adapter should expose a queue consumer entrypoint.</summary>
    public bool IncludeQueue { get; init; }

    /// <summary>Whether the adapter should expose an inbound email entrypoint.</summary>
    public bool IncludeEmail { get; init; }

    /// <summary>Whether the adapter should expose a tail event entrypoint.</summary>
    public bool IncludeTail { get; init; }

    /// <summary>Whether the adapter should expose Durable Object entrypoint helpers.</summary>
    public bool IncludeDurableObjects { get; init; }

    /// <summary>Whether the adapter should expose Workers platform binding and helper APIs.</summary>
    public bool IncludePlatformApis { get; init; } = true;

    /// <summary>Creates runtime adapter options from a discovered build manifest.</summary>
    internal static RuntimeAdapterOptions FromManifest(BuildManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new RuntimeAdapterOptions
        {
            IncludeFetch = manifest.Entrypoints.Any(static entrypoint => entrypoint.Kind == EntrypointKind.Fetch),
            IncludeScheduled = manifest.Entrypoints.Any(static entrypoint => entrypoint.Kind == EntrypointKind.Scheduled),
            IncludeQueue = manifest.Entrypoints.Any(static entrypoint => entrypoint.Kind == EntrypointKind.Queue),
            IncludeEmail = manifest.Entrypoints.Any(static entrypoint => entrypoint.Kind == EntrypointKind.Email),
            IncludeTail = manifest.Entrypoints.Any(static entrypoint => entrypoint.Kind == EntrypointKind.Tail),
            IncludeDurableObjects = manifest.DurableObjects.Count > 0
        };
    }
}
