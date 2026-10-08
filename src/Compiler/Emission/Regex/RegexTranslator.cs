using System.Globalization;
using System.Text.RegularExpressions;

// A .NET regular expression translated at compile time to a JavaScript one with the same matches. Groups maps
// each .NET group number to its JavaScript group index, and Names holds the .NET group names by number.
internal sealed record TranslatedRegex(string Pattern, string Flags, int[] Groups, string[] Names);

// Parses the .NET pattern syntax and emits a JavaScript pattern for the v flag. Constructs are translated to keep
// .NET semantics (\w, \s, \b, ^, $ and . follow .NET's definitions), and constructs whose JavaScript behavior
// would differ, such as captures that JavaScript resets between loop iterations, are rejected.
internal static partial class RegexTranslator
{
    private const RegexOptions SupportedOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline
        | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace
        | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private const string WordClass = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";
    private const string BoundaryWordClass = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}]";
    private const string SpaceClass = @"\t\n\v\f\r\x85\p{Z}";

    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No",
        "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "S", "Sm", "Sc", "Sk", "So",
        "Z", "Zs", "Zl", "Zp", "C", "Cc", "Cf", "Cs", "Co", "Cn"
    };

    public static TranslatedRegex Translate(string pattern, RegexOptions options)
    {
        if ((options & ~SupportedOptions) != 0)
            throw Error($"RegexOptions.{options & ~SupportedOptions} is not supported");
        Regex regex;
        try
        {
            regex = new Regex(pattern, options);
        }
        catch (ArgumentException exception)
        {
            throw Error($"the pattern is invalid: {exception.Message}");
        }

        var parser = new Parser(pattern, options);
        var root = parser.ParseRoot();
        var captures = parser.Captures;
        var names = new List<string> { "0" };
        foreach (var capture in captures.Where(capture => capture.Name is null))
        {
            capture.Number = names.Count;
            names.Add(capture.Number.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var capture in captures.Where(capture => capture.Name is not null))
        {
            if (names.Contains(capture.Name!))
                throw Error($"the group name '{capture.Name}' is used more than once");
            capture.Number = names.Count;
            names.Add(capture.Name!);
        }
        if (!regex.GetGroupNames().SequenceEqual(names)
            || !regex.GetGroupNumbers().SequenceEqual(Enumerable.Range(0, names.Count)))
            throw Error("its group numbering could not be reproduced");

        foreach (var reference in parser.References)
            reference.Target = reference.Name is { } name
                ? captures.SingleOrDefault(capture => capture.Number == names.IndexOf(name))
                : captures.SingleOrDefault(capture => capture.Number == reference.Number);
        Validate(root, []);

        // A pattern whose case-sensitive constructs all share one setting, such as one starting with (?i), uses
        // the i flag for it. Otherwise V8 lets a modifier group's case setting leak into later character classes
        // under the v flag, so every case-sensitive construct states its own setting.
        var cases = CaseSettings(root).Distinct().ToArray();
        var ignoreCase = cases.Length == 1 ? cases[0] : options.HasFlag(RegexOptions.IgnoreCase);
        var emitter = new Emitter(ignoreCase, explicitCase: cases.Length > 1);
        var javaScript = emitter.Emit(root);
        var groups = new int[names.Count];
        foreach (var capture in captures)
            groups[capture.Number] = capture.JavaScriptIndex;
        var flags = (captures.Count != 0 ? "d" : "") + "g" + (ignoreCase ? "i" : "") + "v";
        return new TranslatedRegex(javaScript, flags, groups, [.. names]);
    }

    private static NotSupportedException Error(string reason) =>
        new($"WRK120: This regular expression is not supported because {reason}.");
}
