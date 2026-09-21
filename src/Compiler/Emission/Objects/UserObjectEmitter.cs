using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
internal sealed partial class JavaScriptEmitter
{
    private string UserObject(
        BaseObjectCreationExpressionSyntax source,
        IMethodSymbol? constructor,
        INamedTypeSymbol type,
        ArgumentSyntax[] arguments)
    {
        var typeName = QueueUserType(type, source);
        var creation = PositionalObjectCreation(source, constructor, arguments,
            values => $"new {typeName}({string.Join(", ", values)})");
        if (source.Initializer is null || source.Initializer.Expressions.Count == 0)
            return creation;

        var temporary = _names.Get(
            $"object-initializer:{source.SyntaxTree.FilePath}:{source.SpanStart}",
            "value");
        var assignments = source.Initializer.Expressions.Select(expression => expression switch
        {
            AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax =>
                $"{UserInitializerMemberAccess(temporary, assignment.Left)} = {Expression(assignment.Right)};",
            _ => throw Unsupported("WRK106", expression)
        });
        return $"(({temporary}) => {{ {string.Join(" ", assignments)} return {temporary}; }})({creation})";
    }

    private string UserInitializerMemberAccess(string receiver, ExpressionSyntax expression) =>
        _model.GetSymbolInfo(expression).Symbol is { } symbol
            ? UserMemberAccess(receiver, symbol)
            : throw UnsupportedSymbol(null, expression);

    private string UserInitializerMemberName(ExpressionSyntax expression) =>
        _model.GetSymbolInfo(expression).Symbol is { } symbol
            ? UserMemberName(symbol)
            : throw UnsupportedSymbol(null, expression);

}
