internal enum JavaScriptHelper
{
    WithHeader, Delay, Stream, Socket, Digest, WebSocketEvents, IntegerDivide, IntegerRemainder,
    RandomNext, SetAdd, Base64, RpcArguments, IntParse, HexDecode, EscapeDataString
}

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
        JavaScriptHelper.IntegerRemainder => "integerRemainder",
        JavaScriptHelper.RandomNext => "randomNext",
        JavaScriptHelper.SetAdd => "setAdd",
        JavaScriptHelper.Base64 => "base64Encode",
        JavaScriptHelper.RpcArguments => "rpcArguments",
        JavaScriptHelper.IntParse => "intParse",
        JavaScriptHelper.HexDecode => "hexDecode",
        JavaScriptHelper.EscapeDataString => "escapeDataString",
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };
}
