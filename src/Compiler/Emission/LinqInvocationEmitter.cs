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

    private string LinqOrdering(
        SyntaxNode sourceNode,
        IMethodSymbol method,
        IParameterSymbol[] parameters,
        string source,
        string[] arguments,
        bool descending,
        bool append)
    {
        if (arguments.Length != 1 || parameters.Length != 1
            || parameters[0].Type is not INamedTypeSymbol { DelegateInvokeMethod: { } selector }
            || selector.Parameters.Length != 1)
            throw UnsupportedSymbol(method, sourceNode);
        var keyKind = GetLinqOrderKeyKind(selector.ReturnType);
        if (keyKind is null) throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqOrder, source,
            [arguments[0], descending ? "true" : "false", ((int)keyKind).ToString(), append ? "true" : "false"]);
    }

    private static LinqOrderKeyKind? GetLinqOrderKeyKind(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type.ToDisplayString() is "System.DateTime" or "System.DateTimeOffset")
            return LinqOrderKeyKind.DateTime;
        if (type.SpecialType == SpecialType.System_Char) return LinqOrderKeyKind.Character;
        if (type.TypeKind == TypeKind.Enum || type.SpecialType == SpecialType.System_Boolean
            || type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return LinqOrderKeyKind.Numeric;
        return null;
    }

    private enum LinqOrderKeyKind { Numeric, Character, DateTime }

    private string LinqGroupBy(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || !HasDelegateArity(parameters[0].Type, 1)
            || arguments.Length == 2 && !HasDelegateArity(parameters[1].Type, 1)
            || method.TypeArguments.Length < 2 || !SupportsLinqEquality(method.TypeArguments[1]))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqGroupBy, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null"]);
    }

    private string LinqDictionary(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || parameters.Any(parameter => !HasDelegateArity(parameter.Type, 1))
            || method.TypeArguments.Length < 2
            || method.TypeArguments[1].SpecialType != SpecialType.System_String)
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqToDictionary, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null"]);
    }

    private string LinqLookup(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || parameters.Any(parameter => !HasDelegateArity(parameter.Type, 1))
            || method.TypeArguments.Length < 2 || !SupportsLinqEquality(method.TypeArguments[1]))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqToLookup, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null"]);
    }

    private string LinqNumericAggregate(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length > 1 || parameters.Length != arguments.Length
            || arguments.Length == 1 && !HasDelegateArity(parameters[0].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var numericType = UnwrapNullable(method.ReturnType);
        var kind = numericType.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_Single => 1,
            SpecialType.System_Double => 2,
            _ => -1
        };
        if (kind < 0) throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqNumericAggregate, source,
            [method.Name == "Sum" ? "0" : "1", arguments.Length == 0 ? "null" : arguments[0],
                kind.ToString(), IsNullable(method.ReturnType) ? "true" : "false"]);
    }

    private string LinqExtremum(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        var by = method.Name.EndsWith("By", StringComparison.Ordinal);
        if (arguments.Length > 1 || parameters.Length != arguments.Length
            || by && arguments.Length != 1
            || arguments.Length == 1 && !HasDelegateArity(parameters[0].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var hasSelector = arguments.Length == 1;
        var keyType = hasSelector
            ? ((INamedTypeSymbol)parameters[0].Type).DelegateInvokeMethod!.ReturnType
            : method.ReturnType;
        var kind = GetLinqOrderKeyKind(keyType);
        if (kind is null) throw UnsupportedSymbol(method, sourceNode);
        var canBeNull = IsNullable(method.ReturnType) || method.ReturnType.IsReferenceType;
        return Linq(JavaScriptHelper.LinqExtremum, source,
            [hasSelector ? arguments[0] : "null", method.Name.StartsWith("Max", StringComparison.Ordinal) ? "true" : "false",
                ((int)kind).ToString(), canBeNull ? "true" : "false", by ? "true" : "false",
                hasSelector && !by ? "true" : "false"]);
    }

    private string LinqSet(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        var by = method.Name.EndsWith("By", StringComparison.Ordinal);
        if (arguments.Length != (by ? 2 : 1) || parameters.Length != arguments.Length
            || by && !HasDelegateArity(parameters[1].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var keyType = by
            ? ((INamedTypeSymbol)parameters[1].Type).DelegateInvokeMethod!.ReturnType
            : method.TypeArguments[0];
        if (!SupportsLinqEquality(keyType)) throw UnsupportedSymbol(method, sourceNode);
        var mode = method.Name.StartsWith("Union", StringComparison.Ordinal) ? 0
            : method.Name.StartsWith("Intersect", StringComparison.Ordinal) ? 1 : 2;
        return Linq(JavaScriptHelper.LinqSet, source,
            [arguments[0], by ? arguments[1] : "null", mode.ToString(), by && mode != 0 ? "true" : "false"]);
    }

    private string LinqDefaultIfEmpty(
        SyntaxNode sourceNode, IMethodSymbol method, string source, string[] arguments)
    {
        if (arguments.Length > 1) throw UnsupportedSymbol(method, sourceNode);
        var defaultValue = arguments.Length == 1 ? arguments[0] : DefaultFieldValue(method.TypeArguments[0], sourceNode);
        return Linq(JavaScriptHelper.LinqDefaultIfEmpty, source, [defaultValue]);
    }

    private string LinqZip(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length != 2 || parameters.Length != 2 || !HasDelegateArity(parameters[1].Type, 2))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqZip, source, arguments);
    }

    private string LinqAggregate(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is < 1 or > 3 || parameters.Length != arguments.Length)
            throw UnsupportedSymbol(method, sourceNode);
        var seedless = arguments.Length == 1;
        var accumulatorIndex = seedless ? 0 : 1;
        if (!HasDelegateArity(parameters[accumulatorIndex].Type, 2)
            || arguments.Length == 3 && !HasDelegateArity(parameters[2].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqAggregate, source,
            [arguments[accumulatorIndex], seedless ? "null" : arguments[0], seedless ? "false" : "true",
                arguments.Length == 3 ? arguments[2] : "null"]);
    }

    private string LinqJoin(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length != 4 || parameters.Length != 4
            || !HasDelegateArity(parameters[1].Type, 1)
            || !HasDelegateArity(parameters[2].Type, 1)
            || !HasDelegateArity(parameters[3].Type, 2)
            || method.TypeArguments.Length < 3 || !SupportsLinqEquality(method.TypeArguments[2]))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqJoin, source,
            [arguments[0], arguments[1], arguments[2], arguments[3], method.Name == "GroupJoin" ? "true" : "false"]);
    }

    private static bool IsNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) => IsNullable(type)
        ? ((INamedTypeSymbol)type).TypeArguments[0]
        : type;

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

    private static bool HasDelegateArity(ITypeSymbol type, int parameterCount) =>
        type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
        && invoke.Parameters.Length == parameterCount;

    private static bool HasInt32(IParameterSymbol[] parameters, string[] arguments) =>
        arguments.Length == 1 && parameters is [{ Type.SpecialType: SpecialType.System_Int32 }];

    private static bool SupportsLinqEquality(ITypeSymbol type)
    {
        type = UnwrapNullable(type);
        return type.TypeKind == TypeKind.Enum
        || type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String
            or >= SpecialType.System_SByte and <= SpecialType.System_Decimal
        || type.ToDisplayString() == "System.Guid";
    }
}
