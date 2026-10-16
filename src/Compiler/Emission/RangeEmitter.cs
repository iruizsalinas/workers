using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsIndexType(ITypeSymbol? type) => type?.ToDisplayString() == "System.Index";
    private static bool IsRangeType(ITypeSymbol? type) => type?.ToDisplayString() == "System.Range";
    private string IndexValue(string value, bool fromEnd) =>
        $"{_helpers.Require(JavaScriptHelper.IndexRange)}({value}, {(fromEnd ? "true" : "false")})";
    private string RangeValue(IRangeOperation operation)
    {
        var left = RangeEndpoint(operation.LeftOperand, false);
        var right = RangeEndpoint(operation.RightOperand, true);
        return operation.IsLifted ? $"((start, end) => start == null || end == null ? null : [start, end])({left}, {right})"
            : $"[{left}, {right}]";
    }
    private string RangeEndpoint(IOperation? endpoint, bool end) => endpoint is null
        ? $"[0, {(end ? "true" : "false")}]"
        : IsIndexType(endpoint.Type is { } type ? UnwrapNullable(type) : null) ? Expression(endpoint) : IndexValue(Expression(endpoint), false);
    private string SequenceAccess(IOperation receiver, IOperation index)
    {
        if (!IsRangeType(index.Type))
            return HelperInvocation(JavaScriptHelper.SequenceIndex, [Expression(receiver), Expression(index)]);
        _helpers.Require(JavaScriptHelper.IndexRange);
        var slice = $"{_helpers.Name("sequenceSlice")}({Expression(receiver)}, {Expression(index)}{(ResolveType(receiver.Type!) is IArrayTypeSymbol ? ", true" : "")})";
        return ResolveType(receiver.Type!) is IArrayTypeSymbol array ? CopySequenceValues(array.ElementType, slice) : slice;
    }
}
