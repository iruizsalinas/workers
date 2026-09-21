using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string GuidInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ToString", 0) => receiver,
            ("ToString", 1) when SupportedGuidFormat(source, method) =>
                HelperInvocation(JavaScriptHelper.GuidFormat, [receiver, arguments[0]]),
            ("Equals", 1) when method.Parameters[0].Type.ToDisplayString() == "System.Guid" =>
                $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string JsonElementInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ToString", 0) => HelperInvocation(JavaScriptHelper.JsonElementToString, [receiver]),
            ("GetString", 0) => JsonElementValue(receiver, 0),
            ("GetBoolean", 0) => JsonElementValue(receiver, 1),
            ("GetInt32", 0) => JsonElementValue(receiver, 2),
            ("GetDouble", 0) => JsonElementValue(receiver, 3),
            ("GetArrayLength", 0) => JsonElementValue(receiver, 4),
            ("EnumerateArray", 0) => JsonElementValue(receiver, 5),
            ("GetProperty", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                HelperInvocation(JavaScriptHelper.JsonElementGetProperty, [receiver, arguments[0]]),
            ("Clone", 0) => receiver,
            _ => throw UnsupportedSymbol(method, source)
        };

    private string JsonElementValue(string receiver, int operation) =>
        HelperInvocation(JavaScriptHelper.JsonElementGetValue, [receiver, operation.ToString()]);

    private string CancellationTokenInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("ThrowIfCancellationRequested", 0) =>
                HelperInvocation(JavaScriptHelper.CancellationCheck, [receiver]),
            ("Equals", 1) when method.Parameters[0].Type.ToDisplayString()
                                  == "System.Threading.CancellationToken" => $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string CancellationTokenSourceInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments) => (name, arguments.Length) switch
        {
            ("Cancel", 0) => $"{receiver}.abort()",
            ("CancelAfter", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 =>
                HelperInvocation(JavaScriptHelper.CancellationCancelAfter, [receiver, arguments[0]]),
            _ => throw UnsupportedSymbol(method, source)
        };

    private static bool SupportedGuidFormat(InvocationExpressionSyntax source, IMethodSymbol method)
    {
        var format = InvocationArgument(source, method, "format");
        return format is LiteralExpressionSyntax literal
            && (literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression)
                || literal.Token.ValueText is "" or "D" or "d" or "N" or "n" or "B" or "b" or "P" or "p");
    }

    private string NumericToString(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string[] arguments)
    {
        var formatExpression = InvocationArgument(source, method, "format");
        var providerExpression = InvocationArgument(source, method, "provider");
        var providerSymbol = providerExpression is null ? null : _model.GetSymbolInfo(providerExpression).Symbol;
        if (arguments.Length != 2
            || formatExpression is not LiteralExpressionSyntax formatLiteral
            || providerSymbol is not IPropertySymbol
            {
                IsStatic: true,
                Name: "InvariantCulture",
                ContainingType: { } providerType
            }
            || providerType.ToDisplayString() != "System.Globalization.CultureInfo")
            throw UnsupportedSymbol(method, source);
        var format = formatLiteral.Token.ValueText;
        var type = method.ContainingType.SpecialType;
        var integral = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var code = format.Length == 0 ? '\0' : format[0];
        if (integral ? code is not ('D' or 'd' or 'X' or 'x') : code is not ('F' or 'f'))
            throw UnsupportedSymbol(method, source);
        var digits = format.Length == 1 ? (integral ? 0 : 2)
            : int.TryParse(format[1..], out var precision) ? precision : -1;
        if (digits is < 0 or > 100) throw UnsupportedSymbol(method, source);
        var kind = type switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 1,
            SpecialType.System_Single => 2,
            _ => 3
        };
        return HelperInvocation(JavaScriptHelper.NumericFormat,
            [receiver, arguments[0], kind.ToString(), digits.ToString()]);
    }

    private string RandomInvocation(
        SyntaxNode source,
        IMethodSymbol? method,
        string name,
        string[] arguments) => name switch
        {
            "NextDouble" when arguments.Length == 0 => "Math.random()",
            "Next" when arguments.Length == 0 => "Math.floor(Math.random() * 2147483647)",
            "Next" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.RandomNext)}(0, {arguments[0]})",
            "Next" when arguments.Length == 2 =>
                $"{_helpers.Require(JavaScriptHelper.RandomNext)}({arguments[0]}, {arguments[1]})",
            _ => throw UnsupportedSymbol(method, source)
        };

}
