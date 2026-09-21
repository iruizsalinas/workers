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
            : $"{helper}({argument}, {stack.ToString().ToLowerInvariant()}, null)";
    }

    private string CreateTimeSpan(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null || arguments.Length is < 3 or > 5
            || constructor.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_Int32))
            throw UnsupportedSymbol(constructor, source);
        return PositionalObjectCreation(source, constructor, arguments, values =>
        {
            var offset = values.Count == 3 ? 0 : 1;
            var days = values.Count == 3 ? "0" : values[0];
            var milliseconds = values.Count == 5 ? values[4] : "0";
            var total = $"((({days}) * 24 + ({values[offset]})) * 60 + ({values[offset + 1]})) * 60000"
                + $" + ({values[offset + 2]}) * 1000 + ({milliseconds})";
            return $"{_helpers.Require(JavaScriptHelper.TimeSpan)}({total})";
        });
    }

    private string CreateStringBuilder(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null
            || constructor.Parameters.Length > 1
            || constructor.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_String))
            throw UnsupportedSymbol(constructor, source);
        return PositionalObjectCreation(source, constructor, arguments, values =>
            $"{_helpers.Require(JavaScriptHelper.StringBuilder)}({string.Join(", ", values)})");
    }

    private string CreateDateTimeOffset(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null || constructor.Parameters.Length is not (7 or 8)
            || constructor.Parameters[^1].Name != "offset"
            || constructor.Parameters[^1].Type.ToDisplayString() != "System.TimeSpan")
            throw UnsupportedSymbol(constructor, source);

        var offset = arguments.Select((argument, index) => new
            {
                Argument = argument,
                Parameter = ArgumentParameter(constructor, argument, index)
            })
            .SingleOrDefault(item => item.Parameter.Name == "offset")?.Argument.Expression;
        var offsetSymbol = offset is null ? null : _model.GetSymbolInfo(offset).Symbol;
        if (offsetSymbol is null || !offsetSymbol.IsStatic || offsetSymbol.Name != "Zero"
            || offsetSymbol.ContainingType?.ToDisplayString() != "System.TimeSpan")
            throw Unsupported("WRK108", offset ?? source);

        var helper = _helpers.Require(JavaScriptHelper.DateTimeOffset);
        return PositionalObjectCreation(source, constructor, arguments, values =>
            $"{helper}({string.Join(", ", values.Take(values.Count - 1))})");
    }

    private string CreateUrl(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        PositionalObjectCreation(source, constructor, arguments, values => values.Count switch
        {
            1 => $"new URL({values[0]})",
            2 when constructor?.ContainingType.ToDisplayString() == "System.Uri"
                   && constructor.Parameters[0].Type.ToDisplayString() == "System.Uri"
                   && constructor.Parameters[1].Type.SpecialType == SpecialType.System_String =>
                $"new URL({values[1]}, {values[0]})",
            2 when constructor?.ContainingType.ToDisplayString() == "Workers.Url" => $"new URL({values[0]}, {values[1]})",
            _ => throw UnsupportedSymbol(constructor, source)
        });

}
