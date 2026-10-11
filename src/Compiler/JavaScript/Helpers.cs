internal enum JavaScriptHelper
{
    WithHeader, RequestCookies, SetCookie, TaskWhenAll, StreamFrom, SequenceIndex, DictionaryIndex, IntegerDivide, IntegerRemainder,
    SetAdd, QueueStack, RpcArguments, JsonExpect, TryCall, CollectionMembers, StringReplace,
    StringOrdinal,
    Router,
    NumericParse, LinqValues, LinqWhere, LinqSelect, LinqSelectMany, LinqAppend, LinqPrepend, LinqSkip, LinqTake,
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
        if (helper is JavaScriptHelper.CollectionMembers or JavaScriptHelper.QueueStack)
            _required.Add(JavaScriptHelper.LinqValues);
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
        JavaScriptHelper.RequestCookies => "requestCookies",
        JavaScriptHelper.SetCookie => "setCookie",
        JavaScriptHelper.TaskWhenAll => "taskWhenAll",
        JavaScriptHelper.StreamFrom => "streamFrom",
        JavaScriptHelper.SequenceIndex => "sequenceIndex",
        JavaScriptHelper.DictionaryIndex => "dictionaryIndex",
        JavaScriptHelper.IntegerDivide => "integerDivide",
        JavaScriptHelper.IntegerRemainder => "integerRemainder",
        JavaScriptHelper.SetAdd => "setAdd",
        JavaScriptHelper.QueueStack => "queueStackCreate",
        JavaScriptHelper.RpcArguments => "rpcArguments",
        JavaScriptHelper.JsonExpect => "jsonExpect",
        JavaScriptHelper.TryCall => "tryCall",
        JavaScriptHelper.CollectionMembers => "collectionKey",
        JavaScriptHelper.Router => "routerCreate",
        JavaScriptHelper.NumericParse => "numericParse",
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
