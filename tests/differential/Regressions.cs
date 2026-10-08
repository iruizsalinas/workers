// Each scenario returns a string that must be identical when run on the CLR and when compiled to
// JavaScript. The runtime suite compares every entry against the CLR baseline.
public static class RegressionScenarios
{
    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        results["integerChains"] = IntegerChains();
        results["mixedNumericChains"] = MixedNumericChains();
        results["negation"] = Negation();
        results["unsignedChains"] = UnsignedChains();
        results["exceptionMessages"] = ExceptionMessages();
        results["typedCatch"] = TypedCatch();
        results["catchFilters"] = CatchFilters();
        results["frameworkExceptionMessages"] = FrameworkExceptionMessages();
        results["methodGroups"] = MethodGroups();
        results["staticState"] = StaticState();
        results["memberMutation"] = MemberMutation();
        RegressionLanguageScenarios.Add(results);
        return results;
    }

    private static string IntegerChains()
    {
        int a = 1, b = 1, c = 2;
        var sum = a + b + c;
        var mixed = a - b + c * 3 - a;
        var compound = 5;
        compound += a + b;
        return $"{sum} {mixed} {compound} {a + b + c + sum}";
    }

    private static string MixedNumericChains()
    {
        int a = 2, b = 3, done = 3, total = 4;
        double plus = a + b + 0.5;
        double minus = a + b - 1.5;
        double scaled = (a - b) * 1.5;
        double percent = done * 100.0 / total;
        return $"{plus} {minus} {scaled} {percent} {a + b > 4.5}";
    }

    private static string Negation()
    {
        int count = 3;
        double divisor = 2;
        double product = -count * 0.5;
        return $"{product} {-count / divisor} {-count + 10} {-count - -count} {-(count + 1) * 2}";
    }

    private static string UnsignedChains()
    {
        uint a = 1, b = 1, c = 2;
        uint large = 4000000000;
        return $"{a + b + c} {large + large} {large * 2 + 1}";
    }

    private static string ExceptionMessages()
    {
        var messages = new List<string>();
        try { throw new RegressionError("E1", "bad thing"); }
        catch (Exception exception) { messages.Add(exception.Message); }
        try { throw new RegressionPlainError(); }
        catch (Exception exception) { messages.Add(exception.Message); }
        try { throw new RegressionWrappedError("outer", new InvalidOperationException("inner")); }
        catch (Exception exception) { messages.Add(exception.Message); }
        return string.Join("|", messages);
    }

    private static string TypedCatch()
    {
        var results = new List<string>();
        try { throw new RegressionError("E1", "typed"); }
        catch (RegressionError error) { results.Add($"{error.Code}:{error.Message}"); }
        try { throw new InvalidOperationException("general"); }
        catch (RegressionError error) { results.Add(error.Code); }
        catch (Exception exception) { results.Add(exception.Message); }
        try
        {
            try { throw new RegressionError("E3", "rethrown"); }
            catch (RegressionError) { throw; }
        }
        catch (RegressionError error) { results.Add(error.Code); }
        var log = "";
        try
        {
            try { throw new RegressionError("E4", "finally"); }
            catch (RegressionError) { log += "c"; }
            finally { log += "f"; }
        }
        catch (Exception) { log += "x"; }
        results.Add(log);
        return string.Join("|", results);
    }

    private static string CatchFilters()
    {
        var results = new List<string>();
        try { throw new RegressionError("E2", "filtered"); }
        catch (RegressionError error) when (error.Code == "E1") { results.Add("e1"); }
        catch (RegressionError error) when (error.Code == "E2") { results.Add("e2"); }
        try
        {
            try { throw new InvalidOperationException("bad"); }
            catch (Exception exception) when (exception.Message == "good") { results.Add("wrong"); }
        }
        catch (Exception exception) { results.Add("outer " + exception.Message); }
        return string.Join("|", results);
    }

    private static string MethodGroups()
    {
        List<int> values = [1, 2, 3, 4];
        var formatter = new RegressionFormatter("#");
        return $"{string.Join(",", values.Where(IsEven).Select(Label))} " +
               $"{string.Join(",", values.Select(formatter.Format))} {formatter.All(values)} {values.Aggregate(Add)}";
    }

    private static bool IsEven(int value) => value % 2 == 0;
    private static string Label(int value) => $"<{value}>";
    private static int Add(int left, int right) => left + right;

    private static string StaticState()
    {
        RegressionRegistry.Register("alpha");
        RegressionRegistry.Register("beta");
        RegressionRegistry.Hits++;
        RegressionRegistry.Hits += 2;
        return $"{RegressionRegistry.Count} {RegressionRegistry.Last} {RegressionRegistry.Hits} " +
               $"{RegressionRegistry.Greeting} {RegressionRegistry.Defaults.Count} {RegressionRegistry.Origin}";
    }

    private static string MemberMutation()
    {
        var box = new RegressionBox();
        box.Count++;
        box.Count += 5;
        box.Name += "!";
        return $"{box.Count} {box.Name}";
    }

    private static string FrameworkExceptionMessages()
    {
        var messages = new List<string>();
        try { throw new ArgumentNullException("name"); }
        catch (Exception exception) { messages.Add(exception.Message); }
        try { throw new ArgumentException("Bad value", "amount"); }
        catch (Exception exception) { messages.Add(exception.Message); }
        try { throw new ArgumentOutOfRangeException(paramName: "n", message: "too big"); }
        catch (Exception exception) { messages.Add(exception.Message); }
        try { throw new InvalidOperationException(); }
        catch (Exception exception) { messages.Add(exception.Message); }
        return string.Join("|", messages);
    }
}

public sealed class RegressionError : Exception
{
    public RegressionError(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class RegressionPlainError : Exception
{
}

public sealed class RegressionWrappedError : Exception
{
    public RegressionWrappedError(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class RegressionFormatter
{
    private readonly string _prefix;

    public RegressionFormatter(string prefix)
    {
        _prefix = prefix;
    }

    public string Format(int value) => _prefix + value.ToString();

    public string All(List<int> values) => string.Join("", values.Select(Format));
}

public static class RegressionRegistry
{
    public const int Limit = 10;
    private static readonly List<string> Items = new();
    public static readonly List<string> Defaults = ["one", "two"];
    public static readonly string Origin = "https://example.com/" + Defaults[1];
    public static readonly string Greeting;

    static RegressionRegistry()
    {
        Greeting = "limit-" + Limit.ToString();
    }

    public static int Hits { get; set; }
    public static int Count => Items.Count;
    public static string Last => Items[Items.Count - 1];

    public static void Register(string name)
    {
        Items.Add(name);
    }
}

public sealed class RegressionBox
{
    public int Count { get; set; }
    public string Name { get; set; } = "box";
}
