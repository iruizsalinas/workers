using Workers;

namespace LanguageProfile;

public enum FeatureState { Ready }

public sealed class Greeting(string prefix)
{
    public Greeting() : this("Hello") { }
    public string Format(string name) => $"{prefix}, {name}";
}

public static class LanguageFeatures
{
    private static int Total(params int[] values) => values.Sum();
    private static string Format(object value) => $"{value}";

    public static Response Run()
    {
        var calls = 0;
        (int Low, int High) Bounds(params int[] values)
        {
            calls++;
            return (values.Min(), values.Max());
        }
        var (low, high) = Bounds(1, 3, 7);
        var greeting = new Greeting().Format("Ada");
        var values = new[] { low, 3, high };
        var middle = values[1..^1];
        var sum = Total(low, high);
        object result = new Success<int>(sum);
        FeatureState? state = FeatureState.Ready;
        var invalidRange = false;
        try { var invalid = values[3..1]; }
        catch (Exception) { invalidRange = true; }
        return Response.Json(new { greeting, low, high, middle, sum, calls, text = $"{result}", nullableText = Format(state), invalidRange });
    }
}
