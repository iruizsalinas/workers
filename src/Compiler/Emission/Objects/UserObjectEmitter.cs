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
        return isAsync ? AsyncInitializerBody(source, [temporary], [creation], assignments, temporary)
            : $"(({temporary}) => {{ {assignments} return {temporary}; }})({creation})";
    }

    private string AsyncInitializerBody(SyntaxNode source, IEnumerable<string> parameters, IEnumerable<string> arguments, string body, string result)
    {
        // A skipped await must keep both completion and failures synchronous in the caller.
        if (!_expressionTemporaryScopes.TryPeek(out var temporaries)) throw Unsupported("WRK108", source);
        var state = _names.Get($"initializer-state:{source.SyntaxTree.FilePath}:{source.SpanStart}", "initializerState");
        temporaries.Add(state);
        var read = _helpers.Require(JavaScriptHelper.AsyncExpressionResult);
        var invocation = $"(async ({string.Join(", ", parameters.Append(state))}) => {{ try {{ {body} {state}.value = {result}; }} "
            + $"catch ($workers$error) {{ {state}.error = $workers$error; {state}.failed = true; }} {state}.done = true; }})"
            + $"({string.Join(", ", arguments.Append(state))})";
        return $"(({state} = {{}}), {state}.pending = {invocation}, {state}.done ? {read}({state}) : "
            + $"(await {state}.pending, {read}({state})))";
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
