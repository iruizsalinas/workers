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

    private string LinqSelectMany(
        SyntaxNode sourceNode,
        IMethodSymbol method,
        IParameterSymbol[] parameters,
        string source,
        string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || !IsDelegate(parameters[0].Type)
            || arguments.Length == 2 && !IsDelegate(parameters[1].Type))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqSelectMany, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null", arguments.Length == 2 ? "true" : "false"]);
    }

    private string LinqElement(
        SyntaxNode sourceNode,
        IMethodSymbol method,
        IParameterSymbol[] parameters,
        string source,
        string[] arguments,
        JavaScriptHelper helper,
        bool orDefault)
    {
        string predicate;
        var hasPredicate = false;
        string defaultValue;
        if (arguments.Length == 0)
        {
            predicate = "null";
            defaultValue = DefaultFieldValue(method.ReturnType, sourceNode);
        }
        else if (IsDelegate(parameters[0].Type))
        {
            if (arguments.Length > 2) throw UnsupportedSymbol(method, sourceNode);
            predicate = arguments[0];
            hasPredicate = true;
            defaultValue = arguments.Length == 2
                ? arguments[1]
                : DefaultFieldValue(method.ReturnType, sourceNode);
        }
        else
        {
            if (arguments.Length != 1) throw UnsupportedSymbol(method, sourceNode);
            predicate = "null";
            defaultValue = arguments[0];
        }
        if (!orDefault && arguments.Length > 1)
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(helper, source,
            [predicate, hasPredicate ? "true" : "false", defaultValue, orDefault ? "true" : "false"]);
    }

    private string Linq(JavaScriptHelper helper, string source, IReadOnlyList<string> arguments) =>
        $"{_helpers.Require(helper)}({string.Join(", ", [source, .. arguments])})";

    private static bool HasDelegate(
        IParameterSymbol[] parameters,
        string[] arguments,
        int parameter,
        SpecialType returnType = SpecialType.None) =>
        arguments.Length == 1 && parameters.Length == 1 && IsDelegate(parameters[parameter].Type, returnType);

    private static bool IsDelegate(ITypeSymbol type, SpecialType returnType = SpecialType.None) =>
        type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
        && (returnType == SpecialType.None || invoke.ReturnType.SpecialType == returnType)
        && invoke.Parameters.Length is 1 or 2;

    private static bool HasInt32(IParameterSymbol[] parameters, string[] arguments) =>
        arguments.Length == 1 && parameters is [{ Type.SpecialType: SpecialType.System_Int32 }];

    private static bool SupportsLinqEquality(ITypeSymbol type) => type.TypeKind == TypeKind.Enum
        || type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String
            or >= SpecialType.System_SByte and <= SpecialType.System_Decimal
        || type.ToDisplayString() == "System.Guid";
}
