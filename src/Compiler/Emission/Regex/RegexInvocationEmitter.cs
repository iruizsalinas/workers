using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Regex patterns are compile-time constants translated to JavaScript once, as module-level constants. Match
// and Group values are plain objects, MatchCollection is an array, and GroupCollection is an array of groups.
internal sealed partial class JavaScriptEmitter
{
    private const string RegexNamespace = "System.Text.RegularExpressions";
    private readonly Dictionary<string, string> _regexes = new(StringComparer.Ordinal);
    private readonly StringBuilder _regexDeclarations = new();

    private static bool IsRegexType(ITypeSymbol? type) =>
        type?.ContainingNamespace?.ToDisplayString() == RegexNamespace;

    private static bool IsRegexType(ITypeSymbol? type, string name) =>
        IsRegexType(type) && type!.Name == name;

    // Match, Group and Capture, whose text is their Value.
    private static bool IsRegexCapture(ITypeSymbol? type) =>
        IsRegexType(type) && type!.Name is "Match" or "Group" or "Capture";

    private string RegexConstant(string pattern, RegexOptions options, SyntaxNode source)
    {
        var key = $"{(int)options}:{pattern}";
        if (_regexes.TryGetValue(key, out var existing)) return existing;
        TranslatedRegex translated;
        try
        {
            translated = RegexTranslator.Translate(pattern, options);
        }
        catch (NotSupportedException exception)
        {
            throw Locate(exception, source);
        }
        var name = _names.Get("regex:" + key, "regex");
        _regexDeclarations.Append("const ").Append(name).Append(" = ")
            .Append(_helpers.Require(JavaScriptHelper.Regex)).Append('(')
            .Append(JsonSerializer.Serialize(pattern)).Append(", ")
            .Append(JsonSerializer.Serialize(translated.Pattern)).Append(", ")
            .Append(JsonSerializer.Serialize(translated.Flags)).Append(", ")
            .Append(JsonSerializer.Serialize(translated.Groups)).Append(", ")
            .Append(JsonSerializer.Serialize(translated.Names)).AppendLine(");");
        _regexes.Add(key, name);
        return name;
    }

    // The regex named by a pattern argument and an optional RegexOptions argument, both of which must be constant.
    private string RegexFromArguments(SyntaxNode source, ExpressionSyntax pattern, ExpressionSyntax? options)
    {
        if (_model.GetConstantValue(pattern) is not { HasValue: true, Value: string text })
            throw Locate(new NotSupportedException(
                "WRK120: Regex patterns must be compile-time constants so they can be translated to JavaScript when the Worker is built."), pattern);
        var flags = RegexOptions.None;
        if (options is not null)
        {
            if (_model.GetConstantValue(options) is not { HasValue: true, Value: int value })
                throw Locate(new NotSupportedException("WRK120: RegexOptions must be a compile-time constant."), options);
            flags = (RegexOptions)value;
        }
        return RegexConstant(text, flags, source);
    }

    private static ExpressionSyntax? NamedArgument(
        IMethodSymbol method,
        IReadOnlyList<ArgumentSyntax> arguments,
        string name)
    {
        for (var index = 0; index < arguments.Count; index++)
            if (InvocationParameter(method, arguments[index], index).Name == name)
                return arguments[index].Expression;
        return null;
    }

    private string CreateRegex(BaseObjectCreationExpressionSyntax value, IMethodSymbol? constructor, ArgumentSyntax[] arguments)
    {
        if (constructor is null || constructor.Parameters.Any(parameter => parameter.Name == "matchTimeout")
            || NamedArgument(constructor, arguments, "pattern") is not { } pattern)
            throw UnsupportedSymbol(constructor, value);
        return RegexFromArguments(value, pattern, NamedArgument(constructor, arguments, "options"));
    }

    private string RegexStaticInvocation(InvocationExpressionSyntax invocation, IMethodSymbol method, string[] arguments)
    {
        if (method.Parameters.Any(parameter => parameter.Name == "matchTimeout")
            || method.Parameters.FirstOrDefault()?.Type.SpecialType != SpecialType.System_String
            || NamedArgument(method, invocation.ArgumentList.Arguments, "pattern") is not { } pattern)
            throw UnsupportedSymbol(method, invocation);
        var regex = RegexFromArguments(invocation, pattern, NamedArgument(method, invocation.ArgumentList.Arguments, "options"));
        var values = method.Parameters
            .Where(parameter => parameter.Name is not ("pattern" or "options"))
            .Select(parameter => arguments[parameter.Ordinal])
            .ToArray();
        return RegexOperation(invocation, method, regex, values);
    }

    private string RegexOperation(InvocationExpressionSyntax invocation, IMethodSymbol method, string regex, string[] values)
    {
        string Helper(string name) => $"{RegexHelper(name)}({regex}, {string.Join(", ", values)})";
        var parameters = method.Parameters.Where(parameter => parameter.Name is not ("pattern" or "options"))
            .Select(parameter => parameter.Type.SpecialType).ToArray();
        return (method.Name, parameters) switch
        {
            ("IsMatch", [SpecialType.System_String] or [SpecialType.System_String, SpecialType.System_Int32]) => Helper("regexIsMatch"),
            ("Match", [SpecialType.System_String] or [SpecialType.System_String, SpecialType.System_Int32]) => Helper("regexMatch"),
            ("Matches", [SpecialType.System_String] or [SpecialType.System_String, SpecialType.System_Int32]) => Helper("regexMatches"),
            ("Count", [SpecialType.System_String]) => Helper("regexCount"),
            ("Split", [SpecialType.System_String]) => Helper("regexSplit"),
            ("Replace", [SpecialType.System_String, _] or [SpecialType.System_String, _, SpecialType.System_Int32]) =>
                Helper("regexReplace"),
            _ => throw UnsupportedSymbol(method, invocation)
        };
    }

    private string RegexHelper(string name)
    {
        _helpers.Require(JavaScriptHelper.Regex);
        return _helpers.Name(name);
    }

    private string RegexInstanceInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string receiver,
        string[] arguments) => (method.ContainingType.Name, method.Name, arguments.Length) switch
    {
        ("Regex", "ToString", 0) => $"{receiver}.source",
        ("Regex", "GetGroupNames", 0) => $"{receiver}.names.slice()",
        ("Regex", _, _) => RegexOperation(invocation, method, receiver, arguments),
        ("Match", "NextMatch", 0) => $"{RegexHelper("regexNextMatch")}({receiver})",
        ("Capture", "ToString", 0) => $"{receiver}.value",
        _ => throw UnsupportedSymbol(method, invocation)
    };

    // The JavaScript field for a Match, Group or Capture property, which is the property name in camel case.
    private static string? RegexCaptureField(ISymbol? symbol) =>
        symbol is IPropertySymbol { IsStatic: false, ContainingType: var type, Name: var name }
        && IsRegexType(type)
        && (type.Name, name) is ("Capture", "Value" or "Index" or "Length") or ("Group", "Success" or "Name") or ("Match", "Groups")
            ? LowerFirst(name)
            : null;

    private string RegexMember(MemberAccessExpressionSyntax member, ISymbol symbol)
    {
        if (RegexCaptureField(symbol) is { } field)
            return $"{Expression(member.Expression)}.{field}";
        return (symbol.ContainingType.Name, symbol.Name) switch
        {
            ("Match", "Empty") => $"{RegexHelper("regexEmptyMatch")}()",
            ("GroupCollection" or "MatchCollection", "Count") => $"{Expression(member.Expression)}.length",
            ("GroupCollection", "Values") => $"{Expression(member.Expression)}.slice()",
            ("GroupCollection", "Keys") => $"{Expression(member.Expression)}.map(group => group.name)",
            _ => throw UnsupportedSymbol(symbol, member)
        };
    }

    private string RegexGroup(string groups, string key) => $"{RegexHelper("regexGroup")}({groups}, {key})";

    // A partial method or property implemented by the .NET regex source generator, as the compiled constant.
    private string? GeneratedRegex(ISymbol? symbol, SyntaxNode source)
    {
        if (symbol is not (IMethodSymbol { IsPartialDefinition: true } or IPropertySymbol { IsPartialDefinition: true }))
            return null;
        var attribute = symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == $"{RegexNamespace}.GeneratedRegexAttribute");
        if (attribute is null) return null;
        var location = attribute.ApplicationSyntaxReference?.GetSyntax() ?? source;
        var arguments = attribute.AttributeConstructor?.Parameters
            .Zip(attribute.ConstructorArguments, (parameter, argument) => (parameter.Name, argument.Value))
            .ToDictionary(item => item.Name, item => item.Value) ?? [];
        if (arguments.TryGetValue("matchTimeoutMilliseconds", out var timeout) && timeout is not -1)
            throw Locate(new NotSupportedException("WRK120: Regex match timeouts are not supported."), location);
        if (arguments.TryGetValue("cultureName", out var culture) && culture is string { Length: > 0 })
            throw Locate(new NotSupportedException("WRK120: GeneratedRegex culture names are not supported."), location);
        if (arguments.GetValueOrDefault("pattern") is not string pattern)
            throw Locate(new NotSupportedException("WRK120: GeneratedRegex requires a pattern."), location);
        return RegexConstant(pattern, (RegexOptions)(arguments.GetValueOrDefault("options") as int? ?? 0), location);
    }
}
