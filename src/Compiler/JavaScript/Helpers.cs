internal enum JavaScriptHelper { WithHeader, Delay, Stream, Socket, Digest, WebSocketEvents, IntegerDivide }

internal sealed class HelperRegistry(GeneratedNameAllocator names)
{
    private readonly HashSet<JavaScriptHelper> _required = [];

    public string Require(JavaScriptHelper helper)
    {
        _required.Add(helper);
        return Name(helper.EntryPoint());
    }

    public string Name(string name) => names.Get("helper:" + name, name);

    public string Emit() => string.Concat(_required.Order().Select(helper => HelperSource.Emit(helper, Name)));
}

internal static class JavaScriptHelperExtensions
{
    public static string EntryPoint(this JavaScriptHelper helper) => helper switch
    {
        JavaScriptHelper.WithHeader => "withHeader",
        JavaScriptHelper.Delay => "delay",
        JavaScriptHelper.Stream => "streamRead",
        JavaScriptHelper.Socket => "socketRead",
        JavaScriptHelper.Digest => "digestWriter",
        JavaScriptHelper.WebSocketEvents => "webSocketEvents",
        JavaScriptHelper.IntegerDivide => "integerDivide",
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };
}
