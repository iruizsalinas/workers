using System.Text.RegularExpressions;

// Regular expressions compiled both by the CLR and by Workers: routing, validation, parsing and rewriting, plus the
// .NET-specific semantics of anchors, character classes, group numbering, empty matches and replacement patterns.
public static partial class RegexScenarios
{
    private static readonly Regex Route = new(@"^/users/(?<id>\d+)(?:/(?<section>[a-z]+))?/?$", RegexOptions.IgnoreCase);
    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private static readonly Regex Words = new(@"\b\w+\b");

    [GeneratedRegex(@"(?<key>[A-Za-z_]\w*)\s*=\s*(?<value>""[^""]*""|\S+)")]
    private static partial Regex Assignment();

    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        results["routes"] = Routes();
        results["validation"] = Validation();
        results["unicodeClasses"] = UnicodeClasses();
        results["words"] = WordMatches();
        results["substitutions"] = Substitutions();
        results["evaluators"] = Evaluators();
        results["splits"] = Splits();
        results["anchors"] = Anchors();
        results["dots"] = Dots();
        results["emptyMatches"] = EmptyMatches();
        results["backreferences"] = Backreferences();
        results["atomicAndLookarounds"] = AtomicAndLookarounds();
        results["groupNumbering"] = GroupNumbering();
        results["inlineOptions"] = InlineOptions();
        results["characterClasses"] = CharacterClasses();
        results["failedMatch"] = FailedMatch();
        results["iteration"] = Iteration();
        results["generated"] = Generated();
        results["textConversion"] = TextConversion();
        return results;
    }

    private static string Routes()
    {
        var paths = new[] { "/users/42", "/USERS/7/Posts/", "/users/x", "/users/12/", "/users/5/a1" };
        return string.Join(" | ", paths.Select(path =>
        {
            var match = Route.Match(path);
            return $"{match.Success} {match.Groups["id"].Value} {match.Groups["section"].Success}:{match.Groups["section"].Value}"
                + $" {match.Groups.Count} {match.Groups["section"].Index} {match.Groups[2].Name}";
        }));
    }

    private static string Validation()
    {
        var emails = new[] { "ada@example.com", "bad@", "two@@example.com", "spaced out@example.com", "x@y.z" };
        var valid = emails.Select(email => Email.IsMatch(email) ? "y" : "n");
        return string.Join("", valid)
            + $" {Regex.IsMatch("2024-05-17", @"^\d{4}-\d{2}-\d{2}$")}"
            + $" {Regex.IsMatch("ABC-123", "^[a-z]{3}-[0-9]+$", RegexOptions.IgnoreCase)}"
            + $" {Regex.IsMatch("abc-123", "^[a-z]{3}-[0-9]+$")}"
            + $" {Regex.IsMatch("my-slug_1", @"^[\w-]+$")} {Regex.IsMatch("no slug", @"^[\w-]+$")}"
            + $" {Regex.IsMatch("STRASSE", "strasse", RegexOptions.IgnoreCase)}";
    }

    private static string UnicodeClasses()
    {
        return $"{Regex.IsMatch("café", @"^\w+$")} {Regex.IsMatch("٣٤", @"^\d+$")} {Regex.IsMatch("\u0903", @"^\w$")}"
            + $" {Regex.IsMatch("\u00A0", @"^\s$")} {Regex.IsMatch("\u0085", @"^\s$")} {Regex.IsMatch("\uFEFF", @"^\s$")}"
            + $" {Regex.IsMatch("\u2028", @"^\s$")} {Regex.IsMatch("a\u200Db", @"a\b")}"
            + $" {string.Join(",", Regex.Matches("Hello Ünïcode wörld", @"\p{Lu}\p{Ll}+").Select(match => match.Value))}"
            + $" {Regex.IsMatch("A", @"^\p{Ll}$", RegexOptions.IgnoreCase)} {Regex.IsMatch("A", @"^\p{Ll}$")}"
            + $" {string.Join("/", Regex.Matches("smile 😀 now", @"\S+").Select(match => $"{match.Value}@{match.Index}+{match.Length}"))}";
    }

    private static string WordMatches()
    {
        const string text = "Hello, wörld! It's 2024_v2 already";
        var matches = Words.Matches(text);
        var listed = string.Join(",", matches.Select(match => $"{match.Value}@{match.Index}"));
        var long_ = matches.Where(match => match.Length > 4).Count();
        var fromFive = Words.Match(text, 5);
        return $"{matches.Count} {listed} {long_} {fromFive.Value}@{fromFive.Index} {Words.Count(text)} {matches[1].Value}";
    }

    private static string Substitutions()
    {
        return Regex.Replace("ab", "(a)(b)", "$2$1 $10 ${2} $+ $_ $$ $` $' ${x} $") + " | "
            + Regex.Replace("2024-05-17", @"(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2})", "${d}/${m}/${y} [$&]") + " | "
            + Regex.Replace("one two three", @"\s+", "_") + " | "
            + Regex.Replace("John Smith", @"(\w+)\s(\w+)", "$2, $1") + " | "
            + Regex.Replace("x-1 y-2 z-3", @"(\w)-(\d)", "$2$1", RegexOptions.None) + " | "
            + Regex.Replace("a.b.c", @"\.", "$${dot}") + " | "
            + new Regex("o").Replace("foo boo", "0", 2) + " | "
            + Regex.Replace("abc", "(b)", "$12|$1|${1}2|${1 }");
    }

    private static string Evaluators()
    {
        var titled = Regex.Replace("the quick brown fox", @"\b\w", match => match.Value.ToUpperInvariant());
        var counter = 0;
        var numbered = Words.Replace("alpha beta gamma", match => $"{++counter}:{match.Value}");
        var limited = Words.Replace("alpha beta gamma", match => match.Value.Length.ToString(), 2);
        var doubled = Regex.Replace("price 4 and 10", @"\d+", match => (int.Parse(match.Value) * 2).ToString());
        return $"{titled} | {numbered} | {limited} | {doubled}";
    }

    private static string Splits()
    {
        return string.Join("|", Regex.Split("a1b2c", @"(\d)|(x)")) + " ; "
            + string.Join("|", Regex.Split("abc", "")) + " ; "
            + string.Join("|", Regex.Split("red ,green,  blue", @"\s*,\s*")) + " ; "
            + string.Join("|", Regex.Split("no-separators", ",")) + " ; "
            + string.Join("|", new Regex(@"(-)").Split("a-b-")) + " ; "
            + Regex.Split("", "x").Length;
    }

    private static string Anchors()
    {
        const string lines = "line1\r\nline2\nline3\n";
        return $"{Regex.IsMatch("ab\n", "ab$")} {Regex.IsMatch("ab\r\n", "ab$")} {Regex.IsMatch("ab\n", @"ab\z")} {Regex.IsMatch("ab\n", @"ab\Z")}"
            + $" {Regex.Matches(lines, @"^\w+$", RegexOptions.Multiline).Count}"
            + $" {string.Join(",", Regex.Matches(lines, @"^\w+\r?$", RegexOptions.Multiline).Select(match => match.Value.Length.ToString()))}"
            + $" {Regex.Matches(lines, @"^", RegexOptions.Multiline).Count} {Regex.Matches(lines, @"$", RegexOptions.Multiline).Count}"
            + $" {Regex.Matches(lines, @"$").Count} {Regex.IsMatch("x\ny", @"\Ay")} {Regex.IsMatch("x\ny", @"(?m)^y")}";
    }

    private static string Dots()
    {
        return $"{Regex.IsMatch("a\rb", "a.b")} {Regex.IsMatch("a\nb", "a.b")} {Regex.IsMatch("a\nb", "a.b", RegexOptions.Singleline)}"
            + $" {Regex.IsMatch("a\u2028b", "a.b")} {Regex.IsMatch("a\nb", "(?s)a.b")} {Regex.Match("one\ntwo", ".+").Value}";
    }

    private static string EmptyMatches()
    {
        return $"{Regex.Matches("abc", "x*").Count} {Regex.Replace("abc", "x*", "-")} {Regex.Replace("baa", "a*", "<$&>")}"
            + $" {string.Join(",", Regex.Matches("aa", "a??").Select(match => match.Index.ToString()))}"
            + $" {Regex.Replace("abc", "", ".")} {Regex.Count("hello", "l*")}";
    }

    private static string Backreferences()
    {
        var quoted = Regex.Matches("say \"hi\" and 'bye' or \"mixed'", @"(['""])(.*?)\1");
        var doubled = Regex.Match("this is is a test test", @"\b(\w+)\s+\1\b", RegexOptions.IgnoreCase);
        var tags = Regex.Match("<b>bold</b> <i>x</b>", @"<(?<tag>\w+)>[^<]*</\k<tag>>");
        return $"{string.Join(",", quoted.Select(match => match.Groups[2].Value))} {doubled.Value}@{doubled.Index}"
            + $" {tags.Value} {Regex.IsMatch("abcABC", @"^(abc)\1$", RegexOptions.IgnoreCase)} {Regex.IsMatch("abcABC", @"^(abc)\1$")}";
    }

    private static string AtomicAndLookarounds()
    {
        return $"{Regex.IsMatch("aaab", "(?>a+)ab")} {Regex.IsMatch("aaab", "(?:a+)ab")} {Regex.IsMatch("abc", "(?>a|ab)c")}"
            + $" {string.Join(",", Regex.Matches("cost $12.50 and $3 or 4", @"(?<=\$)\d+(\.\d\d)?").Select(match => match.Value))}"
            + $" {Regex.Replace("1234567", @"\B(?=(\d{3})+(?!\d))", ",")}"
            + $" {string.Join(",", Regex.Matches("foo.js bar.ts baz.js", @"\w+(?=\.js\b)").Select(match => match.Value))}"
            + $" {Regex.IsMatch("password1", @"^(?=.*\d)(?=.*[a-z]).{8,}$")} {Regex.IsMatch("password", @"^(?=.*\d)(?=.*[a-z]).{8,}$")}"
            + $" {Regex.Match("(?>x)y xy", @"(?>x)y").Index}";
    }

    private static string GroupNumbering()
    {
        var match = Regex.Match("on 2024-05-17", @"(?<y>\d{4})-(\d{2})-(?<d>\d{2})");
        var names = string.Join(",", Regex.Match("a-1", @"(?<y>\d)|(a)").Groups.Keys);
        var explicitCapture = Regex.Match("ab", "(a)(?<n>b)", RegexOptions.ExplicitCapture);
        var nested = Regex.Match("abc", "((a)(b))(c)");
        return $"{match.Groups[1].Value} {match.Groups[2].Name}={match.Groups[2].Value} {match.Groups[3].Name}={match.Groups[3].Value}"
            + $" {match.Groups["1"].Value} {match.Groups[9].Success}:{match.Groups[9].Name}: {match.Groups["zz"].Success}"
            + $" {names} {explicitCapture.Groups.Count} {explicitCapture.Groups[1].Name}"
            + $" {string.Join(",", nested.Groups.Values.Select(group => $"{group.Name}={group.Value}@{group.Index}"))}"
            + $" {string.Join(",", Regex.Match("x", "(a)?x").Groups.Values.Select(group => $"{group.Success}:{group.Index}"))}"
            + $" {string.Join(",", new Regex(@"(?<first>\w)(\w)").GetGroupNames())}";
    }

    private static string InlineOptions()
    {
        return $"{Regex.IsMatch("HELLO", "(?i)hello")} {Regex.IsMatch("aBc", "a(?i:b)c")} {Regex.IsMatch("ABc", "a(?i:b)c")}"
            + $" {Regex.IsMatch("ABC", "(?i)a(?-i)b")} {Regex.IsMatch("Abc", "(?i)a(?-i)bc")} {Regex.IsMatch("xY", "x(?i)y|z")}"
            + $" {Regex.IsMatch("xZ", "x(?i)y|z")} {Regex.IsMatch("12 ab", "(?x) \\d+ \\s* # digits\n [a-z]+ $")}"
            + $" {Regex.IsMatch("a b", "a b", RegexOptions.IgnorePatternWhitespace)} {Regex.IsMatch("a b", @"a\ b", RegexOptions.IgnorePatternWhitespace)}"
            + $" {Regex.IsMatch("a#b", "a[#]b", RegexOptions.IgnorePatternWhitespace)} {Regex.Match("ab", "(?n)(a)(?<x>b)").Groups.Count}";
    }

    private static string CharacterClasses()
    {
        return $"{string.Join(",", Regex.Matches("rhythm and blues", "[a-z-[aeiou]]+").Select(match => match.Value))}"
            + $" {Regex.IsMatch("A", "[^a]", RegexOptions.IgnoreCase)} {Regex.IsMatch("]", "[]a]")} {Regex.IsMatch("-", "[a-]")}"
            + $" {Regex.IsMatch("-", @"[\d-x]")} {Regex.IsMatch("^", "[a^]")} {Regex.IsMatch("[", "[[]")} {Regex.IsMatch("\\", @"[\\]")}"
            + $" {Regex.Replace("a.b|c(d)", @"[.|()]", "_")} {Regex.IsMatch("\b", @"[\b]")} {Regex.IsMatch("é", "[^a-z]")}"
            + $" {Regex.IsMatch("x", @"[^\W\d]")} {Regex.IsMatch("5", @"[^\W\d]")} {Regex.IsMatch("\t", @"[\S\t]")}"
            + $" {Regex.IsMatch("B", "[^a-z-[x]]", RegexOptions.IgnoreCase)} {Regex.IsMatch("{", "a{,2}|{")} {Regex.IsMatch("a{,2}", "a{,2}")}"
            + $" {Regex.IsMatch("\u001B[0m", @"\e\[\d+m")} {Regex.IsMatch("\u0001", @"\cA")} {Regex.IsMatch("\n", @"\012")} {Regex.IsMatch("A", @"\x41\u0041?")}";
    }

    private static string FailedMatch()
    {
        var match = Regex.Match("xyz", @"(?<y>\d)(a)");
        var next = match.NextMatch();
        return $"{match.Success} '{match.Value}' {match.Index} {match.Length} {match.Groups.Count} {match.Groups[0].Name}"
            + $" '{match.Groups[1].Name}' {match.Groups["y"].Success} {next.Success} {Match.Empty.Success} {match == Match.Empty}"
            + $" {Regex.Matches("xyz", @"\d").Count} {Regex.Replace("xyz", @"\d", "#")}";
    }

    private static string Iteration()
    {
        var output = new List<string>();
        for (var match = Words.Match("one two  three"); match.Success; match = match.NextMatch())
            output.Add($"{match.Index}:{match.Value}");
        foreach (Match match in Regex.Matches("k1=v1;k2=v2", @"(\w+)=(\w+)"))
        {
            var groups = new List<string>();
            foreach (Group group in match.Groups)
                groups.Add($"{group.Name}={group}");
            output.Add(string.Join("&", groups));
        }
        var lengths = Regex.Matches("a bb ccc", @"\w+").Select(match => match.Length).Sum();
        var firstLong = Regex.Matches("a bb ccc dddd", @"\w+").First(match => match.Length > 2).Value;
        return string.Join(" ", output) + $" {lengths} {firstLong}";
    }

    private static string Generated()
    {
        var matches = Assignment().Matches("name = \"Ada Lovelace\" age=36 _flag =on bad= ");
        var pairs = matches.Select(match => $"{match.Groups["key"].Value}->{match.Groups["value"].Value}");
        return $"{string.Join(";", pairs)} {Assignment()} {Assignment().IsMatch("x=1")}";
    }

    private static string TextConversion()
    {
        var match = Regex.Match("order #4521 shipped", @"#(\d+)");
        var group = match.Groups[1];
        var pattern = new Regex(@"\d+");
        Match? missing = null;
        var described = match is { Success: true, Value.Length: > 2 } ? "long" : "short";
        var empty = Regex.Match("none", @"\d") is { Success: false, Index: 0 } ? "empty" : "found";
        return $"{match} {group} [{missing}] " + match + "/" + group + "/" + pattern + $" {pattern} {match.ToString()} {group.ToString()}"
            + $" {described} {empty} {match?.Groups[1]?.Value} {missing?.Value ?? "null"} {missing?.Groups[1].Value ?? "none"}";
    }
}
