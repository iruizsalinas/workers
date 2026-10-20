using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
internal sealed partial class JavaScriptEmitter
{
    private string CreateQueueOrStack(
        SyntaxNode source,
        IMethodSymbol constructor,
        IReadOnlyList<ArgumentSyntax> arguments,
        bool stack)
    {
        var helper = _helpers.Require(JavaScriptHelper.QueueStack);
        if (arguments.Count == 0)
            return "[]";
        if (arguments.Count != 1)
            throw UnsupportedSymbol(constructor, source);
        var argument = Expression(arguments[0].Expression);
        return constructor.Parameters[0].Type.SpecialType == SpecialType.System_Int32
            ? $"{helper}(null, {stack.ToString().ToLowerInvariant()}, {argument})"
            : CopySequenceValues(constructor.ContainingType.TypeArguments[0], $"{helper}({argument}, {stack.ToString().ToLowerInvariant()}, null)");
    }

    private string CreateUrl(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        PositionalObjectCreation(source, constructor, arguments, values => values.Count switch
        {
            1 => $"new URL({values[0]})",
            2 => $"new URL({values[0]}, ({values[1]}) ?? undefined)",
            _ => throw UnsupportedSymbol(constructor, source)
        });

}
