using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string DateTimeMember(MemberAccessExpressionSyntax member, IPropertySymbol property)
    {
        var receiver = Expression(member.Expression);
        return property.Name switch
        {
            "Year" => $"new Date({receiver}).getUTCFullYear()",
            "Month" => $"new Date({receiver}).getUTCMonth() + 1",
            "Day" => $"new Date({receiver}).getUTCDate()",
            "DayOfWeek" => $"new Date({receiver}).getUTCDay()",
            "DayOfYear" => DateTimeDayOfYear(receiver),
            "Hour" => $"new Date({receiver}).getUTCHours()",
            "Minute" => $"new Date({receiver}).getUTCMinutes()",
            "Second" => $"new Date({receiver}).getUTCSeconds()",
            "Millisecond" => $"new Date({receiver}).getUTCMilliseconds()",
            "Date" => $"new Date(new Date({receiver}).setUTCHours(0, 0, 0, 0))",
            "DateTime" when property.ContainingType.ToDisplayString() == "System.DateTimeOffset" =>
                $"new Date({receiver})",
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private string TimeSpanStaticMember(SyntaxNode source, string name) => name switch
    {
        "Zero" => "0",
        "MaxValue" => TimeSpanLimit(negative: false),
        "MinValue" => TimeSpanLimit(negative: true),
        _ => throw Unsupported("WRK105", source)
    };

    private string TimeSpanLimit(bool negative)
    {
        _helpers.Require(JavaScriptHelper.TimeSpan);
        return (negative ? "-" : "") + _helpers.Name("timeSpanLimit");
    }

    private string TimeSpanMember(MemberAccessExpressionSyntax member, string name)
    {
        var receiver = Expression(member.Expression);
        return name switch
        {
            "Days" => $"Math.trunc(({receiver}) / 86400000)",
            "Hours" => $"Math.trunc(({receiver}) / 3600000) % 24",
            "Minutes" => $"Math.trunc(({receiver}) / 60000) % 60",
            "Seconds" => $"Math.trunc(({receiver}) / 1000) % 60",
            "Milliseconds" => $"Math.trunc({receiver}) % 1000",
            "TotalDays" => $"({receiver}) / 86400000",
            "TotalHours" => $"({receiver}) / 3600000",
            "TotalMinutes" => $"({receiver}) / 60000",
            "TotalSeconds" => $"({receiver}) / 1000",
            "TotalMilliseconds" => receiver,
            _ => throw Unsupported("WRK105", member)
        };
    }

    private string DateTimeDayOfYear(string receiver)
        => $"{_helpers.Require(JavaScriptHelper.DateTimeDayOfYear)}({receiver})";

    private string FormEntryMember(MemberAccessExpressionSyntax member, IPropertySymbol property)
    {
        var receiver = Expression(member.Expression);
        var value = _names.Get(
            $"form-entry:{member.SyntaxTree.FilePath}:{member.SpanStart}",
            "formEntry");
        return property.Name switch
        {
            "File" => $"(({value}) => {value} instanceof File ? {value} : null)({receiver})",
            "Text" => $"(({value}) => typeof {value} === \"string\" ? {value} : null)({receiver})",
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private static bool IsDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.Dictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"));

    private static bool IsSet(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.HashSet<T>" or
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"));

    private static void ThrowIfUnsupportedFrameworkMember(ISymbol? symbol, SyntaxNode source)
    {
        if (symbol?.ContainingNamespace.ToDisplayString() is { } @namespace
            && (@namespace == "System" || @namespace.StartsWith("System.", StringComparison.Ordinal)))
            throw UnsupportedSymbol(symbol, source);
    }

    private static string Response(string[] arguments, string _) => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2)})";
    private string Identifier(IdentifierNameSyntax value) => _model.GetSymbolInfo(value).Symbol switch
    {
        IPropertySymbol { ContainingType: { } type, Name: var name }
            when type.ToDisplayString() == "Workers.WorkerEntrypoint" => name == "Environment" ? "this.env" : "this.ctx",
        IPropertySymbol { IsStatic: false } property when IsUserInstanceType(property.ContainingType) =>
            UserMemberAccess("this", property),
        IFieldSymbol { IsStatic: false } field when IsUserInstanceType(field.ContainingType) =>
            UserMemberAccess("this", field),
        IFieldSymbol { IsStatic: false } field => $"this.{UserIdentifier(field, field.Name)}",
        IFieldSymbol { IsStatic: true, HasConstantValue: true } field => LiteralConstant(field.ConstantValue, value),
        IFieldSymbol { IsStatic: true } => throw Unsupported("WRK110", value),
        ISymbol symbol => UserIdentifier(symbol, value.Identifier),
        _ => value.Identifier.ValueText
    };

    private string LiteralConstant(object? value, SyntaxNode source) => value switch
    {
        null => "null",
        string text => JsonSerializer.Serialize(text),
        char character => JsonSerializer.Serialize(character.ToString()),
        bool boolean => boolean ? "true" : "false",
        byte or sbyte or short or ushort or int or uint or float or double =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        long signed when Math.Abs((double)signed) <= 9_007_199_254_740_991d =>
            signed.ToString(CultureInfo.InvariantCulture),
        ulong unsigned when unsigned <= 9_007_199_254_740_991UL =>
            unsigned.ToString(CultureInfo.InvariantCulture),
        _ => throw Unsupported("WRK108", source)
    };

    private static string ResponseInit(
        string[] arguments,
        int statusIndex,
        int? statusTextIndex = null,
        string? headers = null)
    {
        if (arguments.Length <= statusIndex && headers is null) return "";
        var properties = new List<string>();
        if (arguments.Length > statusIndex) properties.Add($"status: {arguments[statusIndex]}");
        if (statusTextIndex is { } textIndex && arguments.Length > textIndex)
            properties.Add($"statusText: {arguments[textIndex]} ?? undefined");
        if (headers is not null) properties.Add($"headers: {headers}");
        return $", {{ {string.Join(", ", properties)} }}";
    }

    private static string JsonResponseInit(string[] arguments)
    {
        if (arguments.Length == 1) return "";
        var properties = new List<string>();
        if (arguments.Length > 2) properties.Add($"...({arguments[2]} ?? {{}})");
        properties.Add($"status: {arguments[1]}");
        if (arguments.Length > 3) properties.Add($"statusText: {arguments[3]} ?? undefined");
        return $", {{ {string.Join(", ", properties)} }}";
    }
    private string AnonymousMember(AnonymousObjectMemberDeclaratorSyntax value)
    {
        var name = value.NameEquals?.Name.Identifier.ValueText ?? value.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => LowerFirst(member.Name.Identifier.ValueText),
            _ => throw Unsupported("WRK107", value)
        };
        return $"{JavaScriptObjectKey(name)}: {Expression(value.Expression)}";
    }
}
