using Workers;

namespace LanguageProfile;

public sealed record PatternValue(string Text);

public sealed class ProfileConfig
{
    public List<int> Values { get; } = new();
}

public static class ProfileCorrectness
{
    private static int nullArguments;
    private static int NullArgument() => ++nullArguments;
    private static object[] BindingArguments() { nullArguments++; return [1, 2]; }
    public static Response BindingParams(Env env)
    {
        nullArguments = 0;
        var statement = env.D1("DB").Prepare("select ?, ?").Bind(BindingArguments());
        return Response.Json(new { count = nullArguments });
    }
    private static bool IsNullFailure(Action action)
    {
        try { action(); return false; }
        catch (Exception exception) { return exception is NullReferenceException && exception is SystemException; }
    }

    public static Response NullDereferences()
    {
        nullArguments = 0;
        string text = null!; List<int> list = null!; Func<int, int> callback = null!;
        var read = IsNullFailure(() => { var length = text.Length; });
        var call = IsNullFailure(() => text.Contains(NullArgument().ToString()));
        var add = IsNullFailure(() => list.Add(NullArgument()));
        var invoke = IsNullFailure(() => callback(NullArgument()));
        var platformIsNull = false;
        try { var input = new Request("not a URL"); }
        catch (Exception exception) { platformIsNull = exception is NullReferenceException; }
        return Response.Json(new { read, call, add, invoke, nullArguments, platformIsNull });
    }

    private static int formattingCalls;
    private static (int, int) NextTuple() { formattingCalls++; return (formattingCalls, formattingCalls + 1); }
    private static string TypeName<T>() => typeof(T).Name;

    public static Response Run()
    {
        var callbacks = new List<Func<int>>();
        for (var i = 0; i < 3; i++) callbacks.Add(() => i);
        object value = new PatternValue("ready");
        var pattern = value switch { PatternValue { Text.Length: > 0 } => "match", null => "null", _ => "other" };
        string? text = null;
        text ??= "cached";
        var flags = 7; flags &= ~2; flags <<= 1;
        var config = new ProfileConfig { Values = { 1, 2, 3 } };
        var query = from item in config.Values where item > 1 select item * 2;
        var invalid = false; var format = false; var missing = false;
        try { new int[0].First(); } catch (InvalidOperationException) { invalid = true; }
        try { int.Parse("invalid"); } catch (FormatException) { format = true; }
        try { var absent = new Dictionary<string, int>()["missing"]; } catch (KeyNotFoundException) { missing = true; }
        return Response.Json(new { captures = callbacks.Select(callback => callback()).ToArray(), pattern, text, flags,
            query = query.ToArray(), invalid, format, missing, type = TypeName<PatternValue>() });
    }

    public static Response Collections()
    {
        var tuple = (1, 2);
        var list = new List<(int, int)> { tuple };
        List<(int, int)> expression = [tuple];
        tuple.Item1 = 9;
        var array = new[] { (3, 4) };
        var copied = array.ToArray(); var slice = array[..];
        var added = new List<(int, int)>(); added.AddRange(array);
        var projected = array.Select(item => { item.Item1 = 8; return item.Item1; }).ToArray();
        array[0].Item1 = 7;
        var wrapped = false; var inner = false;
        try { new List<int> { 2, 1 }.Sort((a, b) => throw new FormatException("comparison")); }
        catch (InvalidOperationException exception) { wrapped = true; inner = exception.InnerException is FormatException; }
        return Response.Json(new { list = list[0].Item1, expression = expression[0].Item1, copied = copied[0].Item1,
            slice = slice[0].Item1, added = added[0].Item1, projected, wrapped, inner });
    }

    public static Response Enumeration()
    {
        var source = new[] { (1, 2) };
        var boxed = new List<object>();
        foreach (object value in source) boxed.Add(value);
        source[0].Item1 = 9;
        var text = new List<string>();
        foreach (object value in new[] { FeatureState.Ready }) text.Add($"{value}");
        foreach (object value in new Dictionary<string, int> { ["key"] = 3 }) text.Add($"{value}");
        var sum = 0; var invalidCast = false;
        foreach (PatternValue value in new object[] { new PatternValue("ready") }) sum += value.Text.Length;
        try { foreach (PatternValue value in new object[] { "wrong" }) sum += value.Text.Length; }
        catch (InvalidCastException) { invalidCast = true; }
        formattingCalls = 0;
        var tupleText = $"{NextTuple()}";
        return Response.Json(new { boxed = $"{boxed[0]}", text = text.ToArray(), sum, invalidCast, tupleText, formattingCalls });
    }

    public static async Task<Response> Predicates()
    {
        var list = new List<int> { 1 };
        var found = list.Find(value => { if (value == 1) list.Add(2); return value == 2; });
        var everyList = new List<int> { 1 };
        var every = everyList.TrueForAll(value => { if (value == 1) everyList.Add(2); return value == 1; });
        var set = new HashSet<int> { 1, 2 }; var invalidated = false;
        try { foreach (var value in set) { set.Add(3); } }
        catch (InvalidOperationException) { invalidated = true; }
        var predicateNull = false;
        try { list.FindAll(null!); } catch (ArgumentNullException) { predicateNull = true; }
        var input = new Request("https://worker.test/", new RequestInit { Method = "POST", Body = "[{\"text\":\"ready\"},{\"text\":\"ready\"}]" });
        var records = await input.JsonAsync<HashSet<PatternValue>>();
        return Response.Json(new { found, every, invalidated, predicateNull, set,
            recordCount = records!.Count, contains = records.Contains(new PatternValue("ready")) });
    }
}
