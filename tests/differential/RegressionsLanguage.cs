using System.Globalization;
using System.Text;
using System.Text.Json;

// Language and framework semantics that must match the CLR. Each scenario is compiled both by the
// CLR and by Workers; RegressionScenarios.Run collects the results.
public static class RegressionLanguageScenarios
{
    public static void Add(Dictionary<string, string> results)
    {
        results["jsonSerializerContract"] = JsonSerializerContract();
        results["jsonSerializerFormats"] = JsonSerializerFormats();
        results["jsonDeserializeCollections"] = JsonDeserializeCollections();
        results["doubleText"] = DoubleText();
        results["floatText"] = FloatText();
        results["invariantNumberFormats"] = InvariantNumberFormats();
        results["invariantDateFormats"] = InvariantDateFormats();
        results["interpolationAlignment"] = InterpolationAlignment();
        results["parsing"] = Parsing();
        results["invariantCasing"] = InvariantCasing();
        results["stringConcatenation"] = StringConcatenation();
        results["splitOverloads"] = SplitOverloads();
        results["stringBuilderIndexer"] = StringBuilderIndexer();
        results["recordText"] = RecordText();
        results["recordEquality"] = RecordEquality();
        results["recordWith"] = RecordWith();
        results["compoundArithmetic"] = CompoundArithmetic();
        results["casts"] = Casts();
        results["enumText"] = EnumText();
        results["switchExpressions"] = SwitchExpressions();
        results["patternMatching"] = PatternMatching();
        results["patternSwitchStatement"] = PatternSwitchStatement();
        results["tryPattern"] = TryPattern();
        results["dictionaryMembers"] = DictionaryMembers();
        results["listMembers"] = ListMembers();
        results["setMembers"] = SetMembers();
        results["ordinalOrdering"] = OrdinalOrdering();
        results["conditionalAccess"] = ConditionalAccess();
        results["defaultFloatingFields"] = DefaultFloatingFields();
    }

    private static string JsonSerializerContract()
    {
        var item = new RegressionItem("box", 2, 1.5);
        var settings = new RegressionSettings { Name = "s" };
        return JsonSerializer.Serialize(item) + JsonSerializer.Serialize(settings)
            + JsonSerializer.Serialize(new List<RegressionItem> { item })
            + JsonSerializer.Serialize(new { Html = "<a href=\"x\">&'+`</a>", Accent = "caf" + (char)0xE9 });
    }

    private static string JsonSerializerFormats() => JsonSerializer.Serialize(new
    {
        Big = 1e21,
        Small = 1.5e-7,
        NegativeZero = -0.0,
        Single = 1.1f,
        When = new DateTimeOffset(2024, 1, 2, 3, 4, 5, 120, TimeSpan.Zero),
        Duration = TimeSpan.FromMinutes(90),
        Bytes = new byte[] { 1, 2, 3 }
    });

    private static string JsonDeserializeCollections()
    {
        var items = JsonSerializer.Deserialize<List<RegressionItem>>("[{\"Name\":\"a\",\"Count\":1,\"Price\":2},{\"Name\":\"b\",\"Count\":2,\"Price\":0.5}]")!;
        var camel = JsonSerializer.Deserialize<RegressionItem>("{\"name\":\"a\",\"count\":1}")!;
        var settings = JsonSerializer.Deserialize<RegressionSettings>("{\"Name\":\"n\"}")!;
        var rejected = false;
        try { JsonSerializer.Deserialize<RegressionItem>("{\"Name\":\"a\",\"Count\":\"1\"}"); }
        catch (Exception) { rejected = true; }
        return $"{items.Count} {items.Sum(item => item.Count * item.Price)} [{camel.Name}] {camel.Count} {settings.Retries} {settings.Tags.Count} {rejected}";
    }

    private static string DoubleText()
    {
        List<double> values = [1.0 / 3, 100, 1e15, 1e16, 1.2345678901234568e17, 5e-5, 0.0001, -0.0, 1e21, double.MaxValue, double.Epsilon];
        return string.Join(" ", values.Select(value => value.ToString()));
    }

    private static string FloatText()
    {
        float third = 1.0f / 3;
        float rounded = 16777217;
        float tenth = 0.1f;
        double widened = tenth;
        return $"{third} {rounded} {tenth * 3} {widened} {float.MaxValue}";
    }

    private static string InvariantNumberFormats() => FormattableString.Invariant(
        $"{1234.5:N2}|{0.256:P1}|{42:D5}|{255:X}|{3.14159:F3}|{-1234:N0}|{7:F2}|{-0.004:F2}|{1e21:N0}")
        + "|" + (1234567.891).ToString("N2", CultureInfo.InvariantCulture)
        + "|" + (-0.001).ToString("F2", CultureInfo.InvariantCulture);

    private static string InvariantDateFormats()
    {
        var value = new DateTimeOffset(2024, 7, 4, 9, 5, 3, 7, TimeSpan.Zero);
        return string.Join("|", value.ToString("s"), value.ToString("u"), value.ToString("R"),
            value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture),
            value.ToString("dddd, MMM d yyyy h:mm tt", CultureInfo.InvariantCulture),
            value.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture),
            value.ToString(CultureInfo.InvariantCulture),
            string.Create(CultureInfo.InvariantCulture, $"{value:yyyy/MM/dd}"));
    }

    private static string InterpolationAlignment() =>
        $"[{"ab",5}][{"ab",-5}][{42,6}][{true,-6}]" + FormattableString.Invariant($"[{42,6:D3}][{1.5,8:F2}]");

    private static string Parsing()
    {
        var parts = new List<string>();
        foreach (var text in new List<string> { "42", " 7 ", "x", "", "2147483648" })
            parts.Add(int.TryParse(text, out var number) ? $"ok{number}" : $"no{number}");
        var thousands = double.Parse("1,000.5", CultureInfo.InvariantCulture);
        var flag = bool.TryParse("TRUE", out var parsed);
        return $"{string.Join(",", parts)} {thousands} {flag}{parsed}";
    }

    private static string InvariantCasing()
    {
        var sharp = "Stra" + (char)0xDF + "e";
        var dotted = ((char)0x130).ToString();
        var sigma = "" + (char)0x3A3 + (char)0x391 + (char)0x3A3;
        return $"{sharp.ToUpperInvariant()} {dotted.ToLowerInvariant().Length} {sigma.ToLowerInvariant()} {"MiXeD".ToLowerInvariant()}";
    }

    private static string StringConcatenation()
    {
        int count = 5;
        double ratio = 0.1 + 0.2;
        bool flag = true;
        char letter = 'x';
        int? missing = null;
        string? none = null;
        var text = "a";
        text += 1;
        text += false;
        text += 2.5;
        return "count=" + count + " ratio=" + ratio + " flag=" + flag + " letter=" + letter
            + " missing=[" + missing + "] none=[" + none + "] " + text + " " + new RegressionItem("i", 1, 2) + " " + RegressionStatus.Paid;
    }

    private static string SplitOverloads() => string.Join("|", "a:b:c:d".Split(':', 2)) + " "
        + string.Join("|", "k=v=w".Split('=', 2)) + " " + string.Join("|", "a,b,,c".Split(','))
        + " " + string.Join("|", "a--b--c".Split("--")) + " " + "x  y".Split(' ').Length;

    private static string StringBuilderIndexer()
    {
        var builder = new StringBuilder("abc");
        builder.Append("de");
        var rejected = false;
        try { _ = builder[10]; }
        catch (Exception) { rejected = true; }
        return $"{builder[1]}{builder[4]} {rejected}";
    }

    private static string RecordText()
    {
        RegressionItem? missing = null;
        var line = new RegressionLine(new RegressionItem("x", 1, 2.5), null, true, RegressionStatus.Refunded);
        return $"{new RegressionItem("a", 2, 0.5)} | {line} | [{missing}] | {new RegressionEmpty()}";
    }

    private static string RecordEquality()
    {
        var left = new RegressionLine(new RegressionItem("x", 1, double.NaN), null, true, RegressionStatus.Paid);
        var right = new RegressionLine(new RegressionItem("x", 1, double.NaN), null, true, RegressionStatus.Paid);
        RegressionLine? missing = null;
        return $"{left == right} {left != right} {left.Equals(right)} {left == missing} {missing == null} "
            + $"{new RegressionItem("a", 1, 1) == new RegressionItem("a", 2, 1)}";
    }

    private static string RecordWith()
    {
        var item = new RegressionItem("a", 1, 2);
        var changed = item with { Count = 5 };
        var line = new RegressionLine(item, "note", false, RegressionStatus.Pending);
        var updated = line with { Active = true };
        return $"{item.Count} {changed.Count} {changed.Name} {updated.Active} {updated.Note} {updated.Total} {item == changed with { Count = 1 }}";
    }

    private static string CompoundArithmetic()
    {
        int value = 7;
        value *= 3;
        value /= 2;
        value %= 4;
        int large = int.MaxValue;
        large *= 3;
        double ratio = 10;
        ratio /= 4;
        ratio *= 3;
        ratio %= 2;
        uint unsigned = 4000000000;
        unsigned *= 2;
        int negative = -7;
        negative /= 2;
        return $"{value} {large} {ratio} {unsigned} {negative}";
    }

    private static string Casts()
    {
        double positive = 3.99, negative = -3.99, huge = 3e10, nan = double.NaN;
        int wide = 300, minusOne = -1, code = 66;
        uint unsigned = 4000000000;
        int? present = 5;
        return $"{(int)positive} {(int)negative} {(int)huge} {(int)nan} {(uint)negative} {(byte)wide} {(sbyte)wide} "
            + $"{(ushort)minusOne} {(uint)minusOne} {(int)unsigned} {(char)code} {(int)'a'} {(double)wide / 7} "
            + $"{(float)(1.0 / 3)} {(int)RegressionStatus.Refunded} {(RegressionStatus)1} {(int)present}";
    }

    private static string EnumText()
    {
        var status = RegressionStatus.Paid;
        RegressionStatus? missing = null;
        var flags = RegressionAccess.Read | RegressionAccess.Write;
        return $"{status} {(RegressionStatus)7} [{missing}] {status.ToString()} {flags} {RegressionAccess.None} "
            + $"{(RegressionAccess)16} {RegressionAccess.Read | (RegressionAccess)16}";
    }

    private static string SwitchExpressions()
    {
        List<int> numbers = [0, 1, 5, -3, 150];
        var sizes = numbers.Select(number => number switch
        {
            0 => "zero",
            1 or 2 => "small",
            < 0 => "negative",
            >= 100 => "large",
            _ => "other"
        });
        var parity = 7 switch
        {
            var even when even % 2 == 0 => "even",
            var odd when odd > 5 => $"odd-{odd}",
            _ => "odd"
        };
        List<int[]> arrays = [[], [1], [1, 2, 3], [9, 8, 7, 6]];
        var shapes = arrays.Select(array => array switch
        {
            [] => "empty",
            [var single] => $"one{single}",
            [1, .. var rest] => $"rest{rest.Length}",
            [.., var last] => $"last{last}"
        });
        string unmatched;
#pragma warning disable CS8509 // The unmatched value must throw at run time.
        try { unmatched = 5 switch { 1 => "one" }; }
#pragma warning restore CS8509
        catch (Exception) { unmatched = "threw"; }
        return $"{string.Join(",", sizes)} {parity} {string.Join(",", shapes)} {unmatched}";
    }

    private static string PatternMatching()
    {
        var result = new StringBuilder();
        object value = "text";
        if (value is string text && text.Length > 2) result.Append(text);
        int? number = 5;
        if (number is > 3 and < 10) result.Append("|mid");
        if (number is not null) result.Append("|set");
        var line = new RegressionLine(new RegressionItem("x", 12, 1), null, true, RegressionStatus.Paid);
        var kind = line switch
        {
            { Status: RegressionStatus.Paid, Item.Count: > 10 } => "bulk",
            { Status: RegressionStatus.Paid } => "paid",
            _ => "other"
        };
        var positional = new RegressionItem("p", 1, 2) switch { ("p", var count, _) => $"p{count}", _ => "?" };
        Exception error = new RegressionError("E", "typed");
        var typed = error switch { RegressionError regression => regression.Code, _ => "other" };
        return $"{result}|{kind}|{positional}|{typed}";
    }

    private static string PatternSwitchStatement()
    {
        var parts = new List<string>();
        foreach (var value in new List<int> { -1, 0, 4, 50 })
        {
            switch (value)
            {
                case < 0:
                    parts.Add("negative");
                    break;
                case 0:
                    parts.Add("zero");
                    break;
                case var small when small < 10:
                    parts.Add($"small{small}");
                    break;
                default:
                    parts.Add("large");
                    break;
            }
        }
        return string.Join(",", parts);
    }

    private static string TryPattern()
    {
        var stock = new Dictionary<string, int> { ["a"] = 1 };
        var found = stock.TryGetValue("a", out var count);
        var missing = stock.TryGetValue("z", out var none);
        var removed = stock.Remove("a", out var old);
        var guid = Guid.TryParse("00112233-4455-6677-8899-aabbccddeeff", out var id);
        var queue = new Queue<int>();
        var empty = queue.TryDequeue(out var head);
        string? absent = null;
        var parsedNull = int.TryParse(absent, out _);
        return $"{found}{count} {missing}{none} {removed}{old} {guid}{id} {empty}{head} {parsedNull}";
    }

    private static string DictionaryMembers()
    {
        var values = new Dictionary<string, int>();
        values.Add("a", 1);
        values.Add("b", 2);
        var duplicate = false;
        try { values.Add("a", 3); }
        catch (Exception) { duplicate = true; }
        var added = values.TryAdd("a", 9);
        var removed = values.Remove("b");
        var removedAgain = values.Remove("b");
        values["c"] = 3;
        var summary = $"{string.Join(",", values.Keys)} {string.Join(",", values.Values.Select(value => value.ToString()))} "
            + $"{values.ContainsKey("c")}{values.ContainsKey("z")} {values.GetValueOrDefault("z")} {values.GetValueOrDefault("z", 7)}";
        values.Clear();
        return $"{duplicate} {added} {removed}{removedAgain} {summary} {values.Count}";
    }

    private static string ListMembers()
    {
        var values = new List<int> { 5, 1, 4 };
        values.Insert(1, 9);
        var removed = values.Remove(4);
        var missing = values.Remove(100);
        values.AddRange(new List<int> { 7, 7 });
        values.RemoveAt(0);
        var index = values.IndexOf(7);
        var found = values.Find(value => value > 5);
        var notFound = values.Find(value => value > 100);
        var removedAll = values.RemoveAll(value => value == 7);
        var exists = values.Exists(value => value == 9);
        values.Reverse();
        var sorted = new List<double> { 3, double.NaN, -1, 2.5 };
        sorted.Sort();
        var words = new List<string> { "bb", "a", "ccc" };
        words.Sort((left, right) => left.Length - right.Length);
        var items = new List<RegressionItem> { new("a", 1, 1), new("b", 2, 2) };
        var removedRecord = items.Remove(new RegressionItem("a", 1, 1));
        var rangeRejected = false;
        try { values.RemoveAt(10); }
        catch (Exception) { rangeRejected = true; }
        return $"{removed}{missing} {index} {found} {notFound} {removedAll} {exists} {string.Join(",", values.Select(value => value.ToString()))} "
            + $"{string.Join(",", sorted.Select(value => value.ToString()))} {string.Join(",", words)} {removedRecord}{items.Count} {rangeRejected}";
    }

    private static string SetMembers()
    {
        var set = new HashSet<int> { 1, 2, 3 };
        var removed = set.Remove(2);
        set.UnionWith(new List<int> { 3, 4 });
        set.IntersectWith(new List<int> { 1, 4, 5 });
        set.ExceptWith(new List<int> { 1 });
        return $"{removed} {set.Count} {set.Contains(4)}";
    }

    private static string OrdinalOrdering()
    {
        var names = new List<string> { "bob", "Alice", "alice", "Bob", "_x", "10", "9" };
        var ordinal = names.OrderBy(name => name, StringComparer.Ordinal);
        var ignoreCase = names.OrderBy(name => name.Length).ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
        var sorted = new List<string>(names);
        sorted.Sort(StringComparer.Ordinal);
        return $"{string.Join(",", ordinal)} {string.Join(",", ignoreCase)} {string.Join(",", sorted)}";
    }

    private static string ConditionalAccess()
    {
        RegressionLine? missing = null;
        var line = new RegressionLine(new RegressionItem("x", 2, 1), "note", true, RegressionStatus.Paid);
        DateTimeOffset? when = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        DateTimeOffset? never = null;
        return $"[{missing?.Note}] [{line.Note?.ToUpperInvariant()}] [{missing?.Item.Name.Length}] [{line?.Item.Name.Length}] "
            + $"[{when?.ToString("O")}] [{never?.ToString("O")}] [{missing?.Note ?? "none"}]";
    }

    private static string DefaultFloatingFields()
    {
        var reading = new RegressionReading();
        reading.Celsius += 1.5;
        return $"{reading.Celsius} {reading.Ratio} {reading.Label}";
    }
}

public enum RegressionStatus { Pending, Paid, Refunded }

[Flags]
public enum RegressionAccess { None = 0, Read = 1, Write = 2, Execute = 4 }

public sealed record RegressionItem(string Name, int Count, double Price);

public sealed record RegressionLine(RegressionItem Item, string? Note, bool Active, RegressionStatus Status)
{
    public double Total => Item.Count * Item.Price;
}

public sealed record RegressionEmpty();

public sealed class RegressionSettings
{
    public string Name { get; set; } = "";
    public int Retries { get; set; } = 3;
    public List<string> Tags { get; set; } = [];
}

public sealed class RegressionReading
{
    public double Celsius { get; set; }
    public float Ratio { get; set; }
    public string Label { get; set; } = "reading";
}
