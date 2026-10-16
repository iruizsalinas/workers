using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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
            values => UserConstruction(type, constructor!, typeName, values));
        if (source.Initializer is null || source.Initializer.Expressions.Count == 0)
            return creation;

        var temporary = _names.Get(
            $"object-initializer:{source.SyntaxTree.FilePath}:{source.SpanStart}",
            "value");
        var assignments = BoundInitializers((IObjectOrCollectionInitializerOperation)_model.GetOperation(source.Initializer)!, temporary);
        var isAsync = _model.GetOperation(source.Initializer) is { } initializer && ContainsAwait(initializer);
        var initialized = $"({(isAsync ? "async " : "")}({temporary}) => {{ {assignments} return {temporary}; }})({creation})";
        return isAsync ? $"(await {initialized})" : initialized;
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
