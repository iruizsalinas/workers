using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string TupleFieldAccess(string receiver, IFieldSymbol field, SyntaxNode source)
    {
        var fields = field.ContainingType.TupleElements;
        var index = fields.IndexOf(field);
        if (index < 0)
            for (var candidate = 0; candidate < fields.Length; candidate++)
                if (SymbolEqualityComparer.Default.Equals(fields[candidate].CorrespondingTupleField ?? fields[candidate],
                        field.CorrespondingTupleField ?? field))
                {
                    index = candidate;
                    break;
                }
        if (index < 0) throw UnsupportedSymbol(field, source);
        return $"{receiver}[{index}]";
    }

    private string ValueExpression(IOperation operation) => CopyValue(operation.Type, Expression(operation));
    private string ValueExpression(ExpressionSyntax expression) =>
        CopyValue(_model.GetTypeInfo(expression).ConvertedType, Expression(expression));

    private string TupleValue(IEnumerable<string> elements) =>
        $"{_helpers.Require(JavaScriptHelper.Tuple)}([{string.Join(", ", elements)}])";

    private string TupleText(INamedTypeSymbol type, string value, SyntaxNode source) =>
        "((tuple) => \"(\" + " + string.Join(" + \", \" + ", type.TupleElements.Select((field, index) =>
            RecordMemberText(field.Type, $"tuple[{index}]", source))) + $" + \")\")({value})";

    private string CopyValue(ITypeSymbol? type, string value)
    {
        if (type is not null && ResolveType(type) is INamedTypeSymbol pair && IsKeyValuePair(pair))
            return $"((pair) => [{CopyValue(pair.TypeArguments[0], "pair[0]")}, {CopyValue(pair.TypeArguments[1], "pair[1]")}])({value})";
        if (type is not null && ResolveType(type) is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            var copy = CopyValue(nullable.TypeArguments[0], "value");
            return copy == "value" ? value : $"((value) => value == null ? null : {copy})({value})";
        }
        if (type is null || ResolveType(type) is not INamedTypeSymbol { IsTupleType: true } tuple) return value;
        return $"((tuple) => {TupleValue(tuple.TupleElements.Select((field, index) => CopyValue(field.Type, $"tuple[{index}]")))})({value})";
    }

    private string CopySequenceValues(ITypeSymbol type, string values)
    {
        var copy = CopyValue(type, "value");
        return copy == "value" ? values : $"Array.from({values}, value => {copy})";
    }

    private string TupleConversion(INamedTypeSymbol source, INamedTypeSymbol target, string value, SyntaxNode syntax)
    {
        var elements = target.TupleElements.Select((field, index) =>
        {
            var from = source.TupleElements[index].Type;
            var to = field.Type;
            var item = $"tuple[{index}]";
            if (from is INamedTypeSymbol { IsTupleType: true } nestedSource && to is INamedTypeSymbol { IsTupleType: true } nestedTarget)
                return TupleConversion(nestedSource, nestedTarget, item, syntax);
            var conversion = _compilation.ClassifyConversion(from, to);
            return conversion.IsIdentity || conversion.IsReference ? item
                : ForeachElementConversion(from, to, item, syntax, conversion.IsImplicit);
        });
        return $"((tuple) => {TupleValue(elements)})({value})";
    }
}
