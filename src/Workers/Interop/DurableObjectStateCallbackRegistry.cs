using System.Collections.Concurrent;

namespace Workers.Interop;

internal static class DurableObjectStateCallbackRegistry
{
    private static readonly ConcurrentDictionary<string, Func<Task>> Callbacks = new(StringComparer.Ordinal);
    private static long nextCallbackId;

    public static string Retain(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var handle = "do-state:" + Interlocked.Increment(ref nextCallbackId)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        Callbacks[handle] = callback;
        return handle;
    }

    public static void Release(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        Callbacks.TryRemove(handle, out _);
    }

    public static async Task RunAsync(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        if (!Callbacks.TryRemove(handle, out var callback))
            throw new WorkersException($"Durable Object state callback '{handle}' is not defined.");

        await callback();
    }
}
