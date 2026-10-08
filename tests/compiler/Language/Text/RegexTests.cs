using System.Text.RegularExpressions;

namespace Workers.Compiler.Tests;

public sealed class RegexTests
{
    [Fact]
    public void CompilesEachConstantPatternOnceAsAModuleConstant()
    {
        var module = Compile("""
            using System.Text.RegularExpressions;
            using Workers;
            public static class Worker
            {
                private const string Digits = @"^\d+$";
                private static readonly Regex Slug = new(@"^[\w-]+$", RegexOptions.IgnoreCase);

                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    return Response.Json(new
                    {
                        digits = Regex.IsMatch(request.Path, Digits),
                        again = Regex.IsMatch(pattern: Digits, input: request.Path),
                        slug = Slug.IsMatch(request.Path),
                        replaced = Regex.Replace(request.Path, "/+", "/")
                    });
                }
            }
            """);

        Assert.Single(Regex.Matches(module, @"regexCreate\(""\^\\\\d\\u002B\$"""));
        Assert.Contains(@"""^[\\p{Nd}]\u002B(?=\\n?$)"", ""gv""", module);
        Assert.Contains(@"""^[\\p{L}\\p{Mn}\\p{Nd}\\p{Pc}\\-]\u002B(?=\\n?$)"", ""giv""", module);
        Assert.Contains("$workers$regexReplace($workers$regex", module);
    }

    [Theory]
    [InlineData(@"^\d+$", RegexOptions.None, @"^[\p{Nd}]+(?=\n?$)", "gv")]
    [InlineData(@"^a.b$", RegexOptions.Multiline, @"(?<![^\n])a[^\n]b(?![^\n])", "gv")]
    [InlineData(@"a.b", RegexOptions.Singleline | RegexOptions.IgnoreCase, @"a[\s\S]b", "giv")]
    [InlineData(@"\Ax\z|\Z", RegexOptions.None, @"^x$|(?=\n?$)", "gv")]
    [InlineData(@"[a-z-[aeiou]]", RegexOptions.None, @"[[a-z]--[aeiou]]", "gv")]
    [InlineData(@"[^\W\d_]", RegexOptions.None, @"[^[^\p{L}\p{Mn}\p{Nd}\p{Pc}]\p{Nd}_]", "gv")]
    [InlineData(@"[]a-]{,2}", RegexOptions.None, @"[\]a\-]\{,2\}", "gv")]
    [InlineData(@"a(?i)b(?-i:c)", RegexOptions.None, @"(?-i:a)(?i:b)(?:(?-i:c))", "gv")]
    [InlineData(@"(?i)ab", RegexOptions.None, @"ab", "giv")]
    [InlineData(@"(?-i)a\d", RegexOptions.IgnoreCase, @"a[\p{Nd}]", "gv")]
    [InlineData(@"(a*)?b??", RegexOptions.None, @"(?:(a*)|)b??", "dgv")]
    [InlineData(@"(?:a|)??", RegexOptions.None, @"(?:|(?:a|))", "gv")]
    [InlineData(@"(?x) a b # comment", RegexOptions.None, @"ab", "gv")]
    [InlineData(@"(?>a+)b", RegexOptions.None, @"(?:(?=(a+))\1)b", "gv")]
    [InlineData(@"(['""])x\1", RegexOptions.None, @"(['""])x(?:\1)", "dgv")]
    [InlineData(@"a/b\.c\#d", RegexOptions.None, @"a\/b\.c#d", "gv")]
    public void TranslatesDotnetPatternSemantics(string pattern, RegexOptions options, string javaScript, string flags)
    {
        var translated = global::RegexTranslator.Translate(pattern, options);

        Assert.Equal(javaScript, translated.Pattern);
        Assert.Equal(flags, translated.Flags);
    }

    [Fact]
    public void KeepsDotnetGroupNumbering()
    {
        var translated = global::RegexTranslator.Translate(@"(?<year>\d{4})-(\d{2})(?:-(?<day>\d{2}))?(?>(x))?", RegexOptions.None);

        // .NET numbers unnamed groups before named ones; JavaScript numbers every group in order.
        Assert.Equal(["0", "1", "2", "year", "day"], translated.Names);
        Assert.Equal([0, 2, 5, 1, 3], translated.Groups);
    }

    [Theory]
    [InlineData(@"(?:(a)|b)+", "does not participate in every iteration")]
    [InlineData(@"(a)*b|(c?)+", "can match empty text")]
    [InlineData(@"(?:a|b?)+", "can match empty text")]
    [InlineData(@"(?<=(?>a)b)c", "atomic groups inside lookbehinds")]
    [InlineData(@"(a)?b\1", "may not have captured")]
    [InlineData(@"(a)|b\1", "may not have captured")]
    [InlineData(@"(?=(a))\1", "may not have captured")]
    [InlineData(@"(a\1)", "inside the group it refers to")]
    [InlineData(@"\Ga", @"\G")]
    [InlineData(@"(?(a)b|c)", "conditional groups")]
    [InlineData(@"(?<a>x)(?<b-a>y)", "balancing groups")]
    [InlineData(@"\p{IsGreek}", "IsGreek")]
    [InlineData(@"[[:alpha:]]", "POSIX")]
    [InlineData(@"(?<a>x)(?<a>y)", "more than once")]
    [InlineData(@"(", "is invalid")]
    public void RejectsConstructsWhoseJavascriptBehaviorDiffers(string pattern, string reason)
    {
        var error = Assert.Throws<NotSupportedException>(() => global::RegexTranslator.Translate(pattern, RegexOptions.None));

        Assert.StartsWith("WRK120:", error.Message);
        Assert.Contains(reason, error.Message);
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.ECMAScript)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void RejectsOptionsWithoutAJavascriptEquivalent(RegexOptions options)
    {
        var error = Assert.Throws<NotSupportedException>(() => global::RegexTranslator.Translate("a", options));

        Assert.Contains($"RegexOptions.{options}", error.Message);
    }

    [Theory]
    [InlineData("var pattern = request.Path; return Response.Json(Regex.IsMatch(request.Path, pattern));")]
    [InlineData("return Response.Json(Regex.IsMatch(request.Path, GetPattern()));")]
    [InlineData("return Response.Json(Regex.IsMatch(request.Path, $\"^{request.Method}$\"));")]
    [InlineData("var options = RegexOptions.IgnoreCase; return Response.Json(Regex.IsMatch(request.Path, \"a\", options));")]
    [InlineData("return Response.Json(new Regex(GetPattern()).IsMatch(request.Path));")]
    public void RejectsPatternsThatAreNotCompileTimeConstants(string source)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text.RegularExpressions;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    {{source}}
                }

                private static string GetPattern() => "^[0-9]+$";
            }
            """));

        Assert.StartsWith("WRK120:", error.Message);
    }

    [Fact]
    public void ReportsUnsupportedPatternsAtTheirSourceLine()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using System.Text.RegularExpressions;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(Regex.Match(request.Path, "(?:(a)|b)+").Success);
            }
            """));

        Assert.Matches(@"^\(7,\d+\): error WRK120: .*does not participate in every iteration", global::WorkerDiagnostics.Format(error));
    }

    [Theory]
    [InlineData("Regex.Match(request.Path, \"a\", RegexOptions.None, TimeSpan.FromSeconds(1)).Success")]
    [InlineData("new Regex(\"a\").Split(request.Path, 2).Length")]
    [InlineData("new Regex(\"a\").Replace(request.Path, \"b\", 1, 2)")]
    [InlineData("Regex.Match(request.Path, \"(a)\").Groups[1].Captures.Count")]
    [InlineData("Regex.Escape(request.Path)")]
    public void RejectsOverloadsOutsideTheSupportedSubset(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text.RegularExpressions;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json({{expression}});
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Fact]
    public void CompilesGeneratedRegexMethodsAndPropertiesInPartialClasses()
    {
        var module = Compile("""
            using System.Text.RegularExpressions;
            using Workers;
            public static partial class Worker
            {
                private static readonly string Prefix = "id-";

                [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
                private static partial Regex Digits();

                [GeneratedRegex("^[a-z]+$", RegexOptions.IgnoreCase)]
                private static partial Regex Letters { get; }

                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { digits = Digits().IsMatch(request.Path), letters = Worker.Letters.IsMatch(Prefix), plain = Letters.ToString() });
            }
            """);

        Assert.Contains(@"""^[\\p{Nd}]\u002B(?=\\n?$)"", ""gv""", module);
        Assert.Contains(@"""^[a-z]\u002B(?=\\n?$)"", ""giv""", module);
        Assert.DoesNotContain("Digits", module);
    }

    [Fact]
    public void ReportsUnsupportedGeneratedPatternsAtTheirAttribute()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using System.Text.RegularExpressions;
            using Workers;
            public static partial class Worker
            {
                [GeneratedRegex(@"\G\d")]
                private static partial Regex Sticky();

                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(Sticky().IsMatch(request.Path));
            }
            """));

        Assert.Matches(@"^\(5,\d+\): error WRK120: ", global::WorkerDiagnostics.Format(error));
    }
}
