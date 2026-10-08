using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// DateOnly and TimeOnly are their System.Text.Json strings: "yyyy-MM-dd" and "HH:mm:ss" with a
// seven-digit fraction when it is not zero. Both forms are canonical and fixed-width where it matters,
// so equality and ordering are string equality and ordering, and JSON needs no conversion.
internal sealed partial class JavaScriptEmitter
{
    private const string DateOnlyMinimum = "\"0001-01-01\"";
    private const string TimeOnlyMinimum = "\"00:00:00\"";

    private static bool IsDateOnly(ITypeSymbol? type) => type?.ToDisplayString() == "System.DateOnly";
    private static bool IsTimeOnly(ITypeSymbol? type) => type?.ToDisplayString() == "System.TimeOnly";
    private static bool IsDateOrTimeOnly(ITypeSymbol? type) => IsDateOnly(type) || IsTimeOnly(type);

    private string DateOnlyHelper(string function, params string[] arguments) =>
        $"{RequireHelperName(JavaScriptHelper.DateOnly, function)}({string.Join(", ", arguments)})";

    private string TimeOnlyHelper(string function, params string[] arguments) =>
        $"{RequireHelperName(JavaScriptHelper.TimeOnly, function)}({string.Join(", ", arguments)})";

    private string DateOrTimeOnlyMember(MemberAccessExpressionSyntax member, IPropertySymbol property)
    {
        var date = IsDateOnly(property.ContainingType);
        if (property.IsStatic)
            return (date, property.Name) switch
            {
                (true, "MinValue") => DateOnlyMinimum,
                (true, "MaxValue") => "\"9999-12-31\"",
                (false, "MinValue") => TimeOnlyMinimum,
                (false, "MaxValue") => "\"23:59:59.9999999\"",
                _ => throw UnsupportedSymbol(property, member)
            };
        var receiver = Expression(member.Expression);
        string Part(int start, int end) => $"Number(({receiver}).slice({start}, {end}))";
        return (date, property.Name) switch
        {
            (true, "Year") => Part(0, 4),
            (true, "Month") => Part(5, 7),
            (true, "Day") => Part(8, 10),
            (true, "DayNumber") => DateOnlyHelper("dateOnlyDayNumber", receiver),
            (true, "DayOfWeek") => $"(({DateOnlyHelper("dateOnlyDayNumber", receiver)} + 1) % 7)",
            (true, "DayOfYear") => DateOnlyHelper("dateOnlyDayOfYear", receiver),
            (false, "Hour") => Part(0, 2),
            (false, "Minute") => Part(3, 5),
            (false, "Second") => Part(6, 8),
            (false, "Millisecond") => $"Math.floor({TimeOnlyHelper("timeOnlyFraction", receiver)} / 10000)",
            (false, "Microsecond") => $"(Math.floor({TimeOnlyHelper("timeOnlyFraction", receiver)} / 10) % 1000)",
            (false, "Nanosecond") => $"({TimeOnlyHelper("timeOnlyFraction", receiver)} % 10 * 100)",
            (false, "Ticks") => TimeOnlyHelper("timeOnlyTicks", receiver),
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private string DateOrTimeOnlyInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments)
    {
        var date = IsDateOnly(method.ContainingType);
        var sameType = method.Parameters is [{ } parameter]
            && SymbolEqualityComparer.Default.Equals(parameter.Type, method.ContainingType);
        return (date, name, arguments.Length) switch
        {
            (_, "ToString", _) => DateOrTimeOnlyToString(source, method, receiver),
            (_, "Equals", 1) when sameType => $"({receiver} === {arguments[0]})",
            (_, "CompareTo", 1) when sameType =>
                $"((left, right) => left < right ? -1 : left > right ? 1 : 0)({receiver}, {arguments[0]})",
            (true, "AddDays", 1) => DateOnlyHelper("dateOnlyAddDays", receiver, arguments[0]),
            (true, "AddMonths", 1) => DateOnlyHelper("dateOnlyAddMonths", receiver, arguments[0]),
            (true, "AddYears", 1) => DateOnlyHelper("dateOnlyAddYears", receiver, arguments[0]),
            (true, "ToDateTime", 1) => DateOnlyHelper("dateOnlyToDate", receiver, TimeOnlyHelper("timeOnlyTicks", arguments[0])),
            (false, "Add", 1) when HasTimeSpanParameter(method) => TimeOnlyHelper("timeOnlyAdd", receiver, arguments[0], "10000"),
            (false, "AddHours", 1) => TimeOnlyHelper("timeOnlyAdd", receiver, arguments[0], "36000000000"),
            (false, "AddMinutes", 1) => TimeOnlyHelper("timeOnlyAdd", receiver, arguments[0], "600000000"),
            (false, "IsBetween", 2) => TimeOnlyHelper("timeOnlyIsBetween", receiver, arguments[0], arguments[1]),
            (false, "ToTimeSpan", 0) => $"({TimeOnlyHelper("timeOnlyTicks", receiver)} / 10000)",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private string? DateOrTimeOnlyStaticInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string name,
        string[] arguments)
    {
        var date = IsDateOnly(method.ContainingType);
        return (date, name, arguments.Length) switch
        {
            (true, "FromDateTime", 1) => DateOnlyHelper("dateOnlyFromDate", arguments[0]),
            (true, "FromDayNumber", 1) => DateOnlyHelper("dateOnlyFromDayNumber", arguments[0]),
            (false, "FromDateTime", 1) => TimeOnlyHelper("timeOnlyFromDate", arguments[0]),
            (false, "FromTimeSpan", 1) => TimeOnlyHelper("timeOnlyFromTimeSpan", arguments[0]),
            (_, "ParseExact", _) => DateOrTimeOnlyParse(source, method, arguments[0]),
            (_, "Parse" or "TryParse", _) => throw new NotSupportedException(
                $"WRK105: '{method.ToDisplayString()}' depends on the current culture; use {method.ContainingType.Name}.ParseExact(value, " +
                $"\"{(date ? "yyyy-MM-dd" : "HH:mm:ss")}\", CultureInfo.InvariantCulture) or the \"O\" format."),
            _ => null
        };
    }

    private string CreateDateOrTimeOnly(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments)
    {
        if (constructor is null || constructor.Parameters.Length == 0) throw UnsupportedSymbol(constructor, source);
        if (IsDateOnly(constructor.ContainingType))
            return constructor.Parameters.Length == 3
                   && constructor.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32)
                ? PositionalObjectCreation(source, constructor, arguments, values => DateOnlyHelper("dateOnlyCreate", [.. values]))
                : throw UnsupportedSymbol(constructor, source);
        if (constructor.Parameters is [{ Type.SpecialType: SpecialType.System_Int64 }])
            return TimeOnlyHelper("timeOnlyFromTicks", Expression(arguments[0].Expression));
        return constructor.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32)
            ? PositionalObjectCreation(source, constructor, arguments, values => TimeOnlyHelper("timeOnlyCreate", [.. values]))
            : throw UnsupportedSymbol(constructor, source);
    }

    // ==, !=, <, <=, > and >= compare the canonical strings; TimeOnly - TimeOnly is the forward gap.
    private string DateOrTimeOnlyBinary(BinaryExpressionSyntax expression, IBinaryOperation operation)
    {
        var left = Expression(expression.Left);
        var right = Expression(expression.Right);
        var kind = expression.Kind();
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            return $"({left} {BinaryOperator(kind)} {right})";
        string? result = kind switch
        {
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression =>
                $"a {BinaryOperator(kind)} b",
            SyntaxKind.SubtractExpression when IsTimeOnly(operation.OperatorMethod!.ContainingType) =>
                TimeOnlyHelper("timeOnlySubtract", "a", "b"),
            _ => null
        };
        if (result is null) throw UnsupportedSymbol(operation.OperatorMethod, expression);
        var missing = kind == SyntaxKind.SubtractExpression ? "null" : "false";
        return operation.IsLifted
            ? $"((a, b) => a == null || b == null ? {missing} : {result})({left}, {right})"
            : $"((a, b) => {result})({left}, {right})";
    }

    private string DateOrTimeOnlyToString(InvocationExpressionSyntax source, IMethodSymbol method, string receiver)
    {
        var format = InvocationArgument(source, method, "format");
        var provider = InvocationArgument(source, method, "provider");
        var invariant = provider is not null && IsInvariantCulture(provider);
        if (provider is not null && !invariant) throw UnsupportedSymbol(method, source);
        var text = format is null ? null : _model.GetConstantValue(format) is { HasValue: true, Value: string constant } ? constant : null;
        if (format is not null && text is null) throw UnsupportedSymbol(method, source);
        text ??= IsDateOnly(method.ContainingType) ? "d" : "t";
        if (!invariant && !IsCultureInvariantDateFormat(text))
            throw CultureSensitiveFormat(method.ContainingType);
        return DateFormatExpression(method.ContainingType, text, receiver)
            ?? throw new NotSupportedException($"WRK108: The format '{text}' is not supported for '{method.ContainingType.ToDisplayString()}'.");
    }

    private static NotSupportedException CultureSensitiveFormat(ITypeSymbol type) => new(
        $"WRK108: Formatting '{type.ToDisplayString()}' without a format depends on the current culture; use a format such as \"O\", " +
        "or FormattableString.Invariant($\"...\") or string.Create(CultureInfo.InvariantCulture, $\"...\").");

    // DateOnly and TimeOnly formats for the shared dateFormat helper. Standard formats expand to their
    // invariant patterns. Custom patterns may only use fields the type has; others are rejected, as the
    // CLR throws a FormatException for them.
    private static string? DateOrTimeOnlyPattern(string format, bool time)
    {
        if (format.Length == 1)
            return (time, format) switch
            {
                (false, "d") => "MM/dd/yyyy",
                (false, "D") => "dddd, dd MMMM yyyy",
                (false, "m" or "M") => "MMMM dd",
                (false, "o" or "O") => "yyyy'-'MM'-'dd",
                (false, "r" or "R") => "ddd, dd MMM yyyy",
                (false, "y" or "Y") => "yyyy MMMM",
                (true, "t") => "HH:mm",
                (true, "T") => "HH:mm:ss",
                (true, "o" or "O") => "HH':'mm':'ss'.'fffffff",
                (true, "r" or "R") => "HH':'mm':'ss",
                _ => null
            };
        for (var index = 0; index < format.Length;)
        {
            var character = format[index];
            if (character is '\'' or '"')
            {
                var end = format.IndexOf(character, index + 1);
                if (end < 0) return null;
                index = end + 1;
                continue;
            }
            if (character == '\\')
            {
                if (index + 1 == format.Length) return null;
                index += 2;
                continue;
            }
            var count = 1;
            while (index + count < format.Length && format[index + count] == character) count++;
            var allowed = time
                ? character is 'H' or 'h' or 'm' or 's' or 't' || character is 'f' or 'F' && count <= 7
                : character is 'y' or 'M' or 'd';
            if (char.IsAsciiLetter(character) && "yMdgHhmsfFtzK".Contains(character) && !allowed) return null;
            index += count;
        }
        return format;
    }

    // ParseExact and TryParseExact with "O" or, under CultureInfo.InvariantCulture, a fixed-width pattern
    // built from yyyy, MM, dd, HH, mm, ss, f..fffffff and literal punctuation.
    private string DateOrTimeOnlyParse(InvocationExpressionSyntax source, IMethodSymbol method, string value)
    {
        var time = IsTimeOnly(method.ContainingType);
        var parameters = method.Parameters.Where(parameter => parameter.RefKind != RefKind.Out).ToArray();
        if (parameters.Length is not (2 or 4) || parameters[0].Type.SpecialType != SpecialType.System_String
            || parameters[1].Type.SpecialType != SpecialType.System_String)
            throw UnsupportedSymbol(method, source);
        var formatArgument = InvocationArgument(source, method, parameters[1].Name);
        if (formatArgument is null || _model.GetConstantValue(formatArgument) is not { HasValue: true, Value: string format })
            throw new NotSupportedException($"WRK108: {method.Name} needs a constant format string.");
        if (parameters.Length == 4)
        {
            var provider = InvocationArgument(source, method, parameters[2].Name);
            var style = InvocationArgument(source, method, parameters[3].Name);
            if (provider is null || !IsInvariantCulture(provider)
                || style is not null && _model.GetConstantValue(style) is not { HasValue: true, Value: 0 })
                throw new NotSupportedException(
                    $"WRK108: {method.Name} needs CultureInfo.InvariantCulture and DateTimeStyles.None.");
        }
        else if (format is not ("O" or "o"))
            throw new NotSupportedException(
                $"WRK108: {method.Name} with a custom format depends on the current culture; pass CultureInfo.InvariantCulture.");
        var pattern = DateOrTimeOnlyParsePattern(format is "O" or "o"
            ? time ? "HH':'mm':'ss'.'fffffff" : "yyyy'-'MM'-'dd"
            : format, time)
            ?? throw new NotSupportedException(
                $"WRK108: The format '{format}' is not supported for parsing '{method.ContainingType.ToDisplayString()}'.");
        return time
            ? TimeOnlyHelper("timeOnlyParse", value, pattern.Regex, pattern.Fields)
            : DateOnlyHelper("dateOnlyParse", value, pattern.Regex, pattern.Fields);
    }

    private static (string Regex, string Fields)? DateOrTimeOnlyParsePattern(string format, bool time)
    {
        var regex = new StringBuilder("/^");
        var fields = new List<string>();
        void Literal(char character)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character)) throw new FormatException();
            if (@"\^$.*+?()[]{}|/-".Contains(character)) regex.Append('\\');
            regex.Append(character);
        }
        try
        {
            for (var index = 0; index < format.Length;)
            {
                var character = format[index];
                if (character is '\'' or '"')
                {
                    var end = format.IndexOf(character, index + 1);
                    if (end < 0) return null;
                    foreach (var literal in format[(index + 1)..end]) Literal(literal);
                    index = end + 1;
                    continue;
                }
                if (character == '\\')
                {
                    if (index + 1 == format.Length) return null;
                    Literal(format[index + 1]);
                    index += 2;
                    continue;
                }
                var count = 1;
                while (index + count < format.Length && format[index + count] == character) count++;
                var field = (time, character, count) switch
                {
                    (false, 'y', 4) or (false, 'M', 2) or (false, 'd', 2) => character.ToString(),
                    (true, 'H', 2) or (true, 'm', 2) or (true, 's', 2) => character.ToString(),
                    (true, 'f', <= 7) => "f",
                    _ when !char.IsAsciiLetter(character) => null,
                    _ => throw new FormatException()
                };
                if (field is null)
                {
                    for (var repeat = 0; repeat < count; repeat++) Literal(character);
                }
                else
                {
                    if (fields.Contains(field)) return null;
                    fields.Add(field);
                    regex.Append("([0-9]{").Append(count).Append("})");
                }
                index += count;
            }
        }
        catch (FormatException)
        {
            return null;
        }
        var required = time ? new[] { "H", "m" } : ["y", "M", "d"];
        if (required.Any(field => !fields.Contains(field))) return null;
        return (regex.Append("$/").ToString(), "[" + string.Join(", ", fields.Select(field => $"\"{field}\"")) + "]");
    }
}
