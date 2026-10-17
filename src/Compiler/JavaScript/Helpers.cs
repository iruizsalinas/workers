internal enum JavaScriptHelper
{
    RequestCookies, SetCookie, TaskWhenAll, AsyncExpressionResult, StreamFrom, SequenceIndex, IndexRange, DictionaryIndex, IntegerDivide, IntegerRemainder,
    SetAdd, QueueStack, RpcArguments, QueueMessages, JsonExpect, TryCall, CollectionMembers, ListState, StringReplace,
    StringOrdinal, Tuple, Delegates, StructuralKeys, KeyCollectionData, TypeOf, Exceptions, SetState,
    Router, HttpError, Cors, BoundedBody, Redirect,
    NumericParse, LinqValues, LinqWhere, LinqSelect, LinqSelectMany, LinqAppend, LinqPrepend, LinqSkip, LinqTake,
    LinqSkipWhile, LinqTakeWhile, LinqConcat, LinqAny, LinqAll, LinqCount, LinqContains, LinqDistinct,
    LinqDistinctBy, LinqSequenceEqual, LinqOrder, LinqGroupBy, LinqToDictionary, LinqToLookup,
    LinqNumericAggregate, LinqAggregate, LinqExtremum, LinqSet, LinqReverse, LinqDefaultIfEmpty, LinqChunk, LinqZip,
    LinqJoin, LinqElementAt, LinqFirst, LinqLast, LinqSingle, LinqCast, LinqLookup, LinqToArray
}

internal sealed class HelperRegistry(GeneratedNameAllocator names)
{
    private readonly HashSet<JavaScriptHelper> _required = [];

    public string Require(JavaScriptHelper helper)
    {
        _required.Add(helper);
        if (helper is JavaScriptHelper.QueueMessages) Require(JavaScriptHelper.RpcArguments);
        if (helper.IsLinqOperator() || helper is JavaScriptHelper.NumericParse or JavaScriptHelper.CollectionMembers
            or JavaScriptHelper.DictionaryIndex or JavaScriptHelper.StructuralKeys or JavaScriptHelper.SequenceIndex
            or JavaScriptHelper.IndexRange or JavaScriptHelper.IntegerDivide or JavaScriptHelper.IntegerRemainder
            or JavaScriptHelper.ListState or JavaScriptHelper.QueueStack or JavaScriptHelper.Delegates or JavaScriptHelper.TaskWhenAll or JavaScriptHelper.TryCall or JavaScriptHelper.SetState)
            _required.Add(JavaScriptHelper.Exceptions);
        if (helper is JavaScriptHelper.Router) _required.Add(JavaScriptHelper.HttpError);
        if (helper is JavaScriptHelper.BoundedBody) _required.Add(JavaScriptHelper.HttpError);
        if (helper is JavaScriptHelper.CollectionMembers or JavaScriptHelper.QueueStack)
            _required.Add(JavaScriptHelper.LinqValues);
        if (helper is JavaScriptHelper.StructuralKeys) _required.Add(JavaScriptHelper.LinqValues);
        if (helper is JavaScriptHelper.StructuralKeys or JavaScriptHelper.RpcArguments)
            _required.Add(JavaScriptHelper.KeyCollectionData);
        if (helper.IsLinqOperator())
            _required.Add(JavaScriptHelper.LinqValues);
        if (helper is JavaScriptHelper.SequenceIndex or JavaScriptHelper.CollectionMembers)
            _required.Add(JavaScriptHelper.ListState);
        return Name(helper.EntryPoint());
    }

    public string Name(string name) => names.Get("helper:" + name, name);
    public bool IsRequired(JavaScriptHelper helper) => _required.Contains(helper);

    public string Emit() => string.Concat(_required.Order().Select(helper => HelperSource.Emit(helper, Name)));
}

internal static class JavaScriptHelperExtensions
{
    public static string EntryPoint(this JavaScriptHelper helper) => helper switch
    {
        JavaScriptHelper.RequestCookies => "requestCookies",
        JavaScriptHelper.SetCookie => "setCookie",
        JavaScriptHelper.TaskWhenAll => "taskWhenAll",
        JavaScriptHelper.AsyncExpressionResult => "asyncExpressionResult",
        JavaScriptHelper.StreamFrom => "streamFrom",
        JavaScriptHelper.SequenceIndex => "sequenceIndex",
        JavaScriptHelper.IndexRange => "indexCreate",
        JavaScriptHelper.Tuple => "tupleCreate",
        JavaScriptHelper.TypeOf => "typeOf",
        JavaScriptHelper.Exceptions => "exceptionType",
        JavaScriptHelper.SetState => "setCreate",
        JavaScriptHelper.Delegates => "delegateCreate",
        JavaScriptHelper.StructuralKeys => "keyCollection",
        JavaScriptHelper.KeyCollectionData => "keyCollectionKinds",
        JavaScriptHelper.DictionaryIndex => "dictionaryIndex",
        JavaScriptHelper.IntegerDivide => "integerDivide",
        JavaScriptHelper.IntegerRemainder => "integerRemainder",
        JavaScriptHelper.SetAdd => "setAdd",
        JavaScriptHelper.QueueStack => "queueStackCreate",
        JavaScriptHelper.RpcArguments => "rpcArguments",
        JavaScriptHelper.QueueMessages => "queueSend",
        JavaScriptHelper.JsonExpect => "jsonExpect",
        JavaScriptHelper.TryCall => "tryCall",
        JavaScriptHelper.CollectionMembers => "collectionKey",
        JavaScriptHelper.ListState => "listCreate",
        JavaScriptHelper.Router => "routerCreate",
        JavaScriptHelper.HttpError => "HttpError",
        JavaScriptHelper.Cors => "corsAllow",
        JavaScriptHelper.BoundedBody => "requestBytes",
        JavaScriptHelper.Redirect => "redirect",
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
        JavaScriptHelper.LinqCast => "linqCast",
        JavaScriptHelper.LinqLookup => "linqLookup",
        JavaScriptHelper.LinqToArray => "linqToArray",
        _ => throw new ArgumentOutOfRangeException(nameof(helper))
    };

    internal static bool IsLinqOperator(this JavaScriptHelper helper) => helper is
        >= JavaScriptHelper.LinqWhere and <= JavaScriptHelper.LinqToArray;
}
