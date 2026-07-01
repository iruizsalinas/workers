using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    /// <summary>Starts pulling the next chunk from a managed readable stream.</summary>
    [JSExport]
    public static string ManagedReadableStreamPullStart(string handle) =>
        StartManagedInvocation("managedReadableStreamPull", RunWithWorkerContextAsync(() => ManagedReadableStreamPullCoreAsync(handle)));

    /// <summary>Starts cancelling a managed readable stream.</summary>
    [JSExport]
    public static string ManagedReadableStreamCancelStart(string handle) =>
        StartManagedInvocation("managedReadableStreamCancel", RunWithWorkerContextAsync(() => ManagedReadableStreamCancelCoreAsync(handle)));

    private static async Task<string> ManagedReadableStreamPullCoreAsync(string handle)
    {
        var result = await ManagedReadableStreamRegistry.PullAsync(handle);
        var payload = new NativeStreamReadResult(
            result.Done,
            result.Bytes.IsEmpty ? null : Convert.ToBase64String(result.Bytes.Span));
        return JsonSerializer.Serialize(payload, NativeBodyJsonContext.Default.NativeStreamReadResult);
    }

    private static async Task<string> ManagedReadableStreamCancelCoreAsync(string handle)
    {
        await ManagedReadableStreamRegistry.CancelAsync(handle);
        return "";
    }
}
