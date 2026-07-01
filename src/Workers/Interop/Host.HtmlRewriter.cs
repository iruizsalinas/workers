using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    /// <summary>Starts an HTMLRewriter callback dispatch.</summary>
    [JSExport]
    public static string HtmlRewriterCallbackStart(string payloadJson) =>
        StartManagedInvocation("htmlRewriter", RunWithWorkerContextAsync(() => Workers.HtmlRewriterRegistry.InvokeCallbackAsync(payloadJson)));

    /// <summary>Releases an HTMLRewriter handler registry after a native transformed body completes or is cancelled.</summary>
    [JSExport]
    public static void HtmlRewriterRelease(string registryId)
    {
        Workers.HtmlRewriterRegistry.Release(registryId);
    }
}
