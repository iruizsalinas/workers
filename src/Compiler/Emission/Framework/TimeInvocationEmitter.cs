using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string DateTimeInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => name switch
        {
            "ToString" when arguments.Length == 1
                            && source.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax format
                            && format.Token.ValueText is "O" or "o" =>
                method?.ContainingType.ToDisplayString() == "System.DateTime"
                    ? DateTimeRoundTrip(receiver, includeOffset: false)
                    : DateTimeRoundTrip(receiver),
            "AddMonths" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeAddMonths)}({receiver}, {arguments[0]})",
            "AddYears" when arguments.Length == 1 =>
                $"{_helpers.Require(JavaScriptHelper.DateTimeAddMonths)}({receiver}, ({arguments[0]}) * 12)",
            "AddDays" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 86400000"),
            "AddHours" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 3600000"),
            "AddMinutes" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 60000"),
            "AddSeconds" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, $"({arguments[0]}) * 1000"),
            "AddMilliseconds" when arguments.Length == 1 =>
                DateTimeAddMilliseconds(receiver, arguments[0]),
            "Add" when HasTimeSpanParameter(method) => DateTimeAddMilliseconds(receiver, arguments[0]),
            "Subtract" when HasTimeSpanParameter(method) => DateTimeAddMilliseconds(receiver, $"-({arguments[0]})"),
            "Subtract" when arguments.Length == 1 && HasSameDateParameter(method) =>
                $"new Date({receiver}).getTime() - new Date({arguments[0]}).getTime()",
            "CompareTo" when arguments.Length == 1 && HasSameDateParameter(method) =>
                HelperInvocation(JavaScriptHelper.DateTimeCompare, [receiver, arguments[0]]),
            "Equals" when arguments.Length == 1 && HasSameDateParameter(method) =>
                $"{HelperInvocation(JavaScriptHelper.DateTimeCompare, [receiver, arguments[0]])} === 0",
            "ToUniversalTime" when arguments.Length == 0
                                   && method?.ContainingType.ToDisplayString() == "System.DateTimeOffset" =>
                $"new Date({receiver})",
            "ToUnixTimeMilliseconds" when arguments.Length == 0 => $"new Date({receiver}).getTime()",
            "ToUnixTimeSeconds" when arguments.Length == 0 => $"Math.floor(new Date({receiver}).getTime() / 1000)",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string TimeSpanInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol? method,
        string receiver,
        string name,
        string[] arguments) => name switch
        {
            "Add" when HasTimeSpanParameter(method) => TimeSpanArithmetic(receiver, arguments[0], "+"),
            "Subtract" when HasTimeSpanParameter(method) => TimeSpanArithmetic(receiver, arguments[0], "-"),
            "Negate" when arguments.Length == 0 => TimeSpanNegate(receiver),
            "Duration" when arguments.Length == 0 => TimeSpanDuration(receiver),
            "CompareTo" when HasTimeSpanParameter(method) => TimeSpanCompare(receiver, arguments[0]),
            "Equals" when HasTimeSpanParameter(method) => $"{receiver} === {arguments[0]}",
            _ => throw UnsupportedSymbol(method, source)
        };

    private string TimeSpanArithmetic(string left, string right, string operation) =>
        $"{_helpers.Require(JavaScriptHelper.TimeSpan)}(({left}) {operation} ({right}))";

    private string TimeSpanNegate(string value)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanNegate")}({value})";
    }

    private string TimeSpanDuration(string value)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return $"{_helpers.Name("timeSpanDuration")}({value})";
    }

    private static bool HasSameDateParameter(IMethodSymbol? method) => method is { Parameters.Length: 1 }
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, method.Parameters[0].Type);

    private static bool HasTimeSpanParameter(IMethodSymbol? method) => method is { Parameters: [{ Type: { } type }] }
        && type.ToDisplayString() == "System.TimeSpan";

    private string DateTimeAddMilliseconds(string receiver, string delta) =>
        $"{_helpers.Require(JavaScriptHelper.DateTimeAddMilliseconds)}({receiver}, {delta})";
}
