internal enum JavaScriptHelper
{
    WithHeader, Delay, CancellationCheck, CancellationDelay, CancellationCancelAfter,
    Stream, Socket, Digest, WebSocketEvents, SequenceIndex, DictionaryIndex, IntegerDivide, IntegerRemainder,
    RandomNext, SetAdd, Base64, RpcArguments, HexDecode, EscapeDataString, JsonElementToString,
    JsonElementValueKind, JsonElementGetValue, JsonElementGetProperty, JsonElementGetIndex,
    StringTrim, StringContains, StringStartsWith, StringEndsWith, StringSubstring, StringReplace,
    StringIsNullOrEmpty, StringIsNullOrWhiteSpace, StringJoin, StringOrdinal,
    StringRemove, StringInsert, StringPad, StringToCharArray, StringSplit,
    NumericParse, NumericFormat, MathAbsInt, MathClamp, MathRound, MathSign, GuidParse, GuidFormat,
    DateTimeOffset, DateTimeAddMonths, DateTimeFromUnixTime, DateTimeCompare, DateTimeDayOfYear,
    DateTimeIsLeapYear, DateTimeDaysInMonth, DateTimeAddMilliseconds, TimeSpan,
    LinqValues, LinqWhere, LinqSelect, LinqSelectMany, LinqAppend, LinqPrepend, LinqSkip, LinqTake,
    LinqSkipWhile, LinqTakeWhile, LinqConcat, LinqAny, LinqAll, LinqCount, LinqContains, LinqDistinct,
    LinqDistinctBy, LinqSequenceEqual, LinqOrder, LinqGroupBy, LinqToDictionary, LinqToLookup,
    LinqNumericAggregate, LinqAggregate, LinqExtremum, LinqSet, LinqReverse, LinqDefaultIfEmpty, LinqChunk, LinqZip,
    LinqJoin, LinqElementAt, LinqFirst, LinqLast, LinqSingle, LinqToArray
}

internal sealed class HelperRegistry(GeneratedNameAllocator names)
{
    private readonly HashSet<JavaScriptHelper> _required = [];

    public string Require(JavaScriptHelper helper)
    {
        _required.Add(helper);
        if (helper == JavaScriptHelper.CancellationDelay)
        {
            _required.Add(JavaScriptHelper.Delay);
            _required.Add(JavaScriptHelper.CancellationCheck);
        }
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
        JavaScriptHelper.CancellationCheck => "cancellationCheck",
        JavaScriptHelper.CancellationDelay => "cancellationDelay",
        JavaScriptHelper.CancellationCancelAfter => "cancellationCancelAfter",
        JavaScriptHelper.Stream => "streamRead",
        JavaScriptHelper.Socket => "socketRead",
        JavaScriptHelper.Digest => "digestWriter",
        JavaScriptHelper.WebSocketEvents => "webSocketEvents",
        JavaScriptHelper.SequenceIndex => "sequenceIndex",
        JavaScriptHelper.DictionaryIndex => "dictionaryIndex",
        JavaScriptHelper.IntegerDivide => "integerDivide",
        JavaScriptHelper.IntegerRemainder => "integerRemainder",
        JavaScriptHelper.RandomNext => "randomNext",
        JavaScriptHelper.SetAdd => "setAdd",
        JavaScriptHelper.Base64 => "base64Encode",
        JavaScriptHelper.RpcArguments => "rpcArguments",
        JavaScriptHelper.HexDecode => "hexDecode",
        JavaScriptHelper.EscapeDataString => "escapeDataString",
        JavaScriptHelper.JsonElementToString => "jsonElementToString",
        JavaScriptHelper.JsonElementValueKind => "jsonElementValueKind",
        JavaScriptHelper.JsonElementGetValue => "jsonElementGetValue",
        JavaScriptHelper.JsonElementGetProperty => "jsonElementGetProperty",
        JavaScriptHelper.JsonElementGetIndex => "jsonElementGetIndex",
        JavaScriptHelper.StringTrim => "stringTrim",
        JavaScriptHelper.StringContains => "stringContains",
        JavaScriptHelper.StringStartsWith => "stringStartsWith",
        JavaScriptHelper.StringEndsWith => "stringEndsWith",
        JavaScriptHelper.StringSubstring => "stringSubstring",
        JavaScriptHelper.StringReplace => "stringReplace",
        JavaScriptHelper.StringIsNullOrEmpty => "stringIsNullOrEmpty",
        JavaScriptHelper.StringIsNullOrWhiteSpace => "stringIsNullOrWhiteSpace",
        JavaScriptHelper.StringJoin => "stringJoin",
        JavaScriptHelper.StringOrdinal => "stringOrdinal",
        JavaScriptHelper.StringRemove => "stringRemove",
        JavaScriptHelper.StringInsert => "stringInsert",
        JavaScriptHelper.StringPad => "stringPad",
        JavaScriptHelper.StringToCharArray => "stringToCharArray",
        JavaScriptHelper.StringSplit => "stringSplit",
        JavaScriptHelper.NumericParse => "numericParse",
        JavaScriptHelper.NumericFormat => "numericFormat",
        JavaScriptHelper.MathAbsInt => "mathAbsInt",
        JavaScriptHelper.MathClamp => "mathClamp",
        JavaScriptHelper.MathRound => "mathRound",
        JavaScriptHelper.MathSign => "mathSign",
        JavaScriptHelper.GuidParse => "guidParse",
        JavaScriptHelper.GuidFormat => "guidFormat",
        JavaScriptHelper.DateTimeOffset => "dateTimeOffset",
        JavaScriptHelper.DateTimeAddMonths => "dateTimeAddMonths",
        JavaScriptHelper.DateTimeFromUnixTime => "dateTimeFromUnixTime",
        JavaScriptHelper.DateTimeCompare => "dateTimeCompare",
        JavaScriptHelper.DateTimeDayOfYear => "dateTimeDayOfYear",
        JavaScriptHelper.DateTimeIsLeapYear => "dateTimeIsLeapYear",
        JavaScriptHelper.DateTimeDaysInMonth => "dateTimeDaysInMonth",
        JavaScriptHelper.DateTimeAddMilliseconds => "dateTimeAddMilliseconds",
        JavaScriptHelper.TimeSpan => "timeSpan",
        JavaScriptHelper.LinqValues => "linqValues",
        JavaScriptHelper.LinqWhere => "linqWhere",
        JavaScriptHelper.LinqSelect => "linqSelect",
        JavaScriptHelper.LinqSelectMany => "linqSelectMany",
        JavaScriptHelper.LinqAppend => "linqAppend",
        JavaScriptHelper.LinqPrepend => "linqPrepend",
        JavaScriptHelper.LinqSkip => "linqSkip",
        JavaScriptHelper.LinqTake => "linqTake",
        JavaScriptHelper.LinqSkipWhile => "linqSkipWhile",
        JavaScriptHelper.LinqTakeWhile => "linqTakeWhile",
        JavaScriptHelper.LinqConcat => "linqConcat",
        JavaScriptHelper.LinqAny => "linqAny",
        JavaScriptHelper.LinqAll => "linqAll",
        JavaScriptHelper.LinqCount => "linqCount",
        JavaScriptHelper.LinqContains => "linqContains",
        JavaScriptHelper.LinqDistinct => "linqDistinct",
        JavaScriptHelper.LinqDistinctBy => "linqDistinctBy",
        JavaScriptHelper.LinqSequenceEqual => "linqSequenceEqual",
        JavaScriptHelper.LinqOrder => "linqOrder",
        JavaScriptHelper.LinqGroupBy => "linqGroupBy",
        JavaScriptHelper.LinqToDictionary => "linqToDictionary",
        JavaScriptHelper.LinqToLookup => "linqToLookup",
        JavaScriptHelper.LinqNumericAggregate => "linqNumericAggregate",
        JavaScriptHelper.LinqAggregate => "linqAggregate",
        JavaScriptHelper.LinqExtremum => "linqExtremum",
        JavaScriptHelper.LinqSet => "linqSet",
        JavaScriptHelper.LinqReverse => "linqReverse",
        JavaScriptHelper.LinqDefaultIfEmpty => "linqDefaultIfEmpty",
        JavaScriptHelper.LinqChunk => "linqChunk",
        JavaScriptHelper.LinqZip => "linqZip",
        JavaScriptHelper.LinqJoin => "linqJoin",
        JavaScriptHelper.LinqElementAt => "linqElementAt",
        JavaScriptHelper.LinqFirst => "linqFirst",
        JavaScriptHelper.LinqLast => "linqLast",
        JavaScriptHelper.LinqSingle => "linqSingle",
        JavaScriptHelper.LinqToArray => "linqToArray",
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };

    internal static bool IsLinqOperator(this JavaScriptHelper helper) => helper is
        >= JavaScriptHelper.LinqWhere and <= JavaScriptHelper.LinqToArray;
}
