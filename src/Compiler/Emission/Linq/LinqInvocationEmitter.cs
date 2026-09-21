using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsEnumerableMethod(IMethodSymbol? method) =>
        (method?.ReducedFrom ?? method)?.ContainingType.ToDisplayString() == "System.Linq.Enumerable";

    private string LinqInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string[] arguments,
        MemberAccessExpressionSyntax? member,
        string? receiverOverride)
    {
        var reduced = method.ReducedFrom is not null;
        var source = reduced
            ? receiverOverride ?? Expression(member!.Expression)
            : arguments[0];
        var operationArguments = reduced ? arguments : arguments[1..];
        var parameters = (reduced ? method.Parameters : method.Parameters.Skip(1)).ToArray();
        var name = method.Name;

        return name switch
        {
            "Where" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqWhere, source, operationArguments),
            "Select" when HasDelegate(parameters, operationArguments, 0) =>
                Linq(JavaScriptHelper.LinqSelect, source, operationArguments),
            "SelectMany" => LinqSelectMany(invocation, method, parameters, source, operationArguments),
            "Append" when operationArguments.Length == 1 =>
                Linq(JavaScriptHelper.LinqAppend, source, operationArguments),
            "Prepend" when operationArguments.Length == 1 =>
                Linq(JavaScriptHelper.LinqPrepend, source, operationArguments),
            "Skip" when HasInt32(parameters, operationArguments) =>
                Linq(JavaScriptHelper.LinqSkip, source, operationArguments),
            "Take" when HasInt32(parameters, operationArguments) =>
                Linq(JavaScriptHelper.LinqTake, source, operationArguments),
            "SkipWhile" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqSkipWhile, source, operationArguments),
            "TakeWhile" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqTakeWhile, source, operationArguments),
            "Concat" when operationArguments.Length == 1 =>
                Linq(JavaScriptHelper.LinqConcat, source, operationArguments),
            "Any" when operationArguments.Length == 0 => Linq(JavaScriptHelper.LinqAny, source, ["null", "false"]),
            "Any" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqAny, source, [operationArguments[0], "true"]),
            "All" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqAll, source, operationArguments),
            "Count" when operationArguments.Length == 0 => Linq(JavaScriptHelper.LinqCount, source, ["null", "false"]),
            "Count" when HasDelegate(parameters, operationArguments, 0, SpecialType.System_Boolean) =>
                Linq(JavaScriptHelper.LinqCount, source, [operationArguments[0], "true"]),
            "Contains" when operationArguments.Length == 1 && SupportsLinqEquality(method.TypeArguments[0]) =>
                Linq(JavaScriptHelper.LinqContains, source, operationArguments),
            "Distinct" when operationArguments.Length == 0 && SupportsLinqEquality(method.TypeArguments[0]) =>
                Linq(JavaScriptHelper.LinqDistinct, source, []),
            "DistinctBy" when operationArguments.Length == 1 && parameters.Length == 1
                              && IsDelegate(parameters[0].Type)
                              && method.TypeArguments.Length == 2
                              && SupportsLinqEquality(method.TypeArguments[1]) =>
                Linq(JavaScriptHelper.LinqDistinctBy, source, operationArguments),
            "SequenceEqual" when operationArguments.Length == 1 && SupportsLinqEquality(method.TypeArguments[0]) =>
                Linq(JavaScriptHelper.LinqSequenceEqual, source, operationArguments),
            "OrderBy" => LinqOrdering(invocation, method, parameters, source, operationArguments,
                descending: false, append: false),
            "OrderByDescending" => LinqOrdering(invocation, method, parameters, source, operationArguments,
                descending: true, append: false),
            "ThenBy" => LinqOrdering(invocation, method, parameters, source, operationArguments,
                descending: false, append: true),
            "ThenByDescending" => LinqOrdering(invocation, method, parameters, source, operationArguments,
                descending: true, append: true),
            "GroupBy" => LinqGroupBy(invocation, method, parameters, source, operationArguments),
            "ToDictionary" => LinqDictionary(invocation, method, parameters, source, operationArguments),
            "ToLookup" => LinqLookup(invocation, method, parameters, source, operationArguments),
            "Sum" or "Average" => LinqNumericAggregate(invocation, method, parameters, source, operationArguments),
            "Min" or "Max" or "MinBy" or "MaxBy" =>
                LinqExtremum(invocation, method, parameters, source, operationArguments),
            "Union" or "Intersect" or "Except" or "UnionBy" or "IntersectBy" or "ExceptBy" =>
                LinqSet(invocation, method, parameters, source, operationArguments),
            "Reverse" when operationArguments.Length == 0 => Linq(JavaScriptHelper.LinqReverse, source, []),
            "DefaultIfEmpty" => LinqDefaultIfEmpty(invocation, method, source, operationArguments),
            "Chunk" when HasInt32(parameters, operationArguments) =>
                Linq(JavaScriptHelper.LinqChunk, source, operationArguments),
            "Zip" => LinqZip(invocation, method, parameters, source, operationArguments),
            "Aggregate" => LinqAggregate(invocation, method, parameters, source, operationArguments),
            "Join" or "GroupJoin" => LinqJoin(invocation, method, parameters, source, operationArguments),
            "ElementAt" when HasInt32(parameters, operationArguments) =>
                Linq(JavaScriptHelper.LinqElementAt, source, [operationArguments[0], "null", "false"]),
            "ElementAtOrDefault" when HasInt32(parameters, operationArguments) =>
                Linq(JavaScriptHelper.LinqElementAt, source,
                    [operationArguments[0], DefaultFieldValue(method.ReturnType, invocation), "true"]),
            "First" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqFirst, orDefault: false),
            "FirstOrDefault" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqFirst, orDefault: true),
            "Last" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqLast, orDefault: false),
            "LastOrDefault" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqLast, orDefault: true),
            "Single" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqSingle, orDefault: false),
            "SingleOrDefault" => LinqElement(invocation, method, parameters, source, operationArguments,
                JavaScriptHelper.LinqSingle, orDefault: true),
            "ToArray" when operationArguments.Length == 0 => Linq(JavaScriptHelper.LinqToArray, source, []),
            "ToList" when operationArguments.Length == 0 => Linq(JavaScriptHelper.LinqToArray, source, []),
            _ => throw UnsupportedSymbol(method, invocation)
        };
    }

}
