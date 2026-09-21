using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
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
