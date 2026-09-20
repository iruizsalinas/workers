internal enum JavaScriptHelper
{
    WithHeader, Delay, Stream, Socket, Digest, WebSocketEvents, IntegerDivide, IntegerRemainder,
    RandomNext, SetAdd, Base64, RpcArguments, IntParse, HexDecode, EscapeDataString, JsonElementToString,
    DateTimeOffset, DateTimeAddMonths, DateTimeFromUnixTime, DateTimeCompare, DateTimeDayOfYear,
    DateTimeIsLeapYear, DateTimeDaysInMonth, DateTimeAddMilliseconds,
    LinqValues, LinqWhere, LinqSelect, LinqSkip, LinqTake, LinqConcat, LinqAny, LinqAll, LinqCount, LinqContains,
    LinqFirst, LinqLast, LinqSingle, LinqToArray
}

internal sealed class HelperRegistry(GeneratedNameAllocator names)
{
    private readonly HashSet<JavaScriptHelper> _required = [];

    public string Require(JavaScriptHelper helper)
    {
        _required.Add(helper);
        if (helper == JavaScriptHelper.DateTimeDaysInMonth)
            _required.Add(JavaScriptHelper.DateTimeIsLeapYear);
        if (helper.IsLinqOperator())
            _required.Add(JavaScriptHelper.LinqValues);
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
        JavaScriptHelper.JsonElementToString => "jsonElementToString",
        JavaScriptHelper.DateTimeOffset => "dateTimeOffset",
        JavaScriptHelper.DateTimeAddMonths => "dateTimeAddMonths",
        JavaScriptHelper.DateTimeFromUnixTime => "dateTimeFromUnixTime",
        JavaScriptHelper.DateTimeCompare => "dateTimeCompare",
        JavaScriptHelper.DateTimeDayOfYear => "dateTimeDayOfYear",
        JavaScriptHelper.DateTimeIsLeapYear => "dateTimeIsLeapYear",
        JavaScriptHelper.DateTimeDaysInMonth => "dateTimeDaysInMonth",
        JavaScriptHelper.DateTimeAddMilliseconds => "dateTimeAddMilliseconds",
        JavaScriptHelper.LinqValues => "linqValues",
        JavaScriptHelper.LinqWhere => "linqWhere",
        JavaScriptHelper.LinqSelect => "linqSelect",
        JavaScriptHelper.LinqSkip => "linqSkip",
        JavaScriptHelper.LinqTake => "linqTake",
        JavaScriptHelper.LinqConcat => "linqConcat",
        JavaScriptHelper.LinqAny => "linqAny",
        JavaScriptHelper.LinqAll => "linqAll",
        JavaScriptHelper.LinqCount => "linqCount",
        JavaScriptHelper.LinqContains => "linqContains",
        JavaScriptHelper.LinqFirst => "linqFirst",
        JavaScriptHelper.LinqLast => "linqLast",
        JavaScriptHelper.LinqSingle => "linqSingle",
        JavaScriptHelper.LinqToArray => "linqToArray",
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };

    internal static bool IsLinqOperator(this JavaScriptHelper helper) => helper is
        >= JavaScriptHelper.LinqWhere and <= JavaScriptHelper.LinqToArray;
}
