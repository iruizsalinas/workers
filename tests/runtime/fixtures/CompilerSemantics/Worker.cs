using Workers;
using System.Text.Json;

namespace CompilerSemantics;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env environment, Context context)
    {
        if (request.Path == "/differential")
            return Response.Json(CoreSemantics.Run());

        if (request.Path == "/collections")
        {
            var values = new List<int> { 2, 4, 6 };
            var total = 0;
            foreach (var value in values)
                total += value;
            return Response.Json(new { values.Count, total });
        }

        if (request.Path == "/records")
        {
            var parcel = new Parcel(Count: 3, Label: "priority");
            return Response.Json(parcel);
        }

        if (request.Path == "/coalesce")
        {
            bool? configured = null;
            var accepted = configured ?? request.Method == "POST" || request.Method == "PUT";
            return Response.Json(new { accepted });
        }

        if (request.Path == "/constructors")
        {
            var rewritten = new Request(
                options: new FetchOptions { Method = "POST" },
                url: "https://worker.test/reordered");
            var resolved = new Url(
                baseUrl: "https://worker.test/root/",
                value: "child");
            return Response.Json(new
            {
                rewritten.Method,
                rewritten = rewritten.Url.Path,
                resolved = resolved.ToString()
            });
        }

        if (request.Path == "/conversions")
        {
            var invalidIntegerRejected = false;
            var invalidHexRejected = false;
            try
            {
                int.Parse("12abc");
            }
            catch (Exception)
            {
                invalidIntegerRejected = true;
            }
            try
            {
                Convert.FromHexString("0g");
            }
            catch (Exception)
            {
                invalidHexRejected = true;
            }
            return Response.Json(new
            {
                parsed = int.Parse(" +42 "),
                hex = Convert.ToHexString(Convert.FromHexString("00fF")),
                escaped = Uri.EscapeDataString("!*'()"),
                invalidIntegerRejected,
                invalidHexRejected
            });
        }

        if (request.Path == "/constants")
        {
            var state = ParcelState.Ready;
            return Response.Json(new
            {
                state,
                ready = state == ParcelState.Ready,
                limit = ParcelLimits.Maximum
            });
        }

        if (request.Path == "/timestamps")
        {
            var timestamp = DateTimeOffset.UtcNow;
            return Response.Json(new
            {
                interpolated = $"{timestamp:O}",
                explicitFormat = timestamp.ToString("O")
            });
        }

        if (request.Path == "/date-range")
        {
            var rejected = false;
            try
            {
                new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero).AddDays(10);
            }
            catch (Exception)
            {
                rejected = true;
            }
            return Response.Json(new { rejected });
        }

        if (request.Path == "/linq-errors")
        {
            List<int> empty = [];
            List<int> multiple = [1, 2];
            var emptyRejected = false;
            var multipleRejected = false;
            var nullSourceRejected = false;
            var indexRejected = false;
            var emptyAggregateRejected = false;
            var emptyAverageRejected = false;
            var duplicateKeyRejected = false;
            var invalidChunkRejected = false;
            var sumOverflowRejected = false;
            try
            {
                empty.First();
            }
            catch (Exception)
            {
                emptyRejected = true;
            }
            try
            {
                multiple.Single();
            }
            catch (Exception)
            {
                multipleRejected = true;
            }
            try
            {
                List<int>? missing = null;
                var query = missing!.Where(value => value > 0);
            }
            catch (Exception)
            {
                nullSourceRejected = true;
            }
            try
            {
                multiple.ElementAt(5);
            }
            catch (Exception)
            {
                indexRejected = true;
            }
            try { empty.Aggregate((left, right) => left + right); }
            catch (Exception) { emptyAggregateRejected = true; }
            try { empty.Average(); }
            catch (Exception) { emptyAverageRejected = true; }
            try { multiple.ToDictionary(value => "same"); }
            catch (Exception) { duplicateKeyRejected = true; }
            try { multiple.Chunk(0); }
            catch (Exception) { invalidChunkRejected = true; }
            try { new List<int> { int.MaxValue, 1 }.Sum(); }
            catch (Exception) { sumOverflowRejected = true; }
            return Response.Json(new {
                emptyRejected, multipleRejected, nullSourceRejected, indexRejected,
                emptyAggregateRejected, emptyAverageRejected, duplicateKeyRejected,
                invalidChunkRejected, sumOverflowRejected
            });
        }

        if (request.Path == "/timespan-errors")
        {
            var factoryRejected = false;
            var additionRejected = false;
            var negationRejected = false;
            var durationRejected = false;
            try { TimeSpan.FromDays(20_000_000); }
            catch (Exception) { factoryRejected = true; }
            try { TimeSpan.MaxValue.Add(TimeSpan.FromMilliseconds(1)); }
            catch (Exception) { additionRejected = true; }
            try { var value = -TimeSpan.MinValue; }
            catch (Exception) { negationRejected = true; }
            try { TimeSpan.MinValue.Duration(); }
            catch (Exception) { durationRejected = true; }
            return Response.Json(new { factoryRejected, additionRejected, negationRejected, durationRejected });
        }

        if (request.Path == "/bcl-errors")
        {
            var delayRejected = false;
            var substringRejected = false;
            var nullSearchRejected = false;
            var emptyReplacementRejected = false;
            var removeRejected = false;
            var insertRejected = false;
            var paddingRejected = false;
            var characterRangeRejected = false;
            var searchRangeRejected = false;
            var integerParseRejected = false;
            var unsignedParseRejected = false;
            var booleanParseRejected = false;
            var absoluteRejected = false;
            var clampRejected = false;
            var roundRejected = false;
            var signRejected = false;
            var guidParseRejected = false;
            string? missing = null;
            try { await Task.Delay(-2); }
            catch (Exception) { delayRejected = true; }
            try { "value".Substring(-1); }
            catch (Exception) { substringRejected = true; }
            try { "value".Contains(missing!); }
            catch (Exception) { nullSearchRejected = true; }
            try { "value".Replace("", "replacement"); }
            catch (Exception) { emptyReplacementRejected = true; }
            try { "value".Remove(6); }
            catch (Exception) { removeRejected = true; }
            try { "value".Insert(-1, "x"); }
            catch (Exception) { insertRejected = true; }
            try { "value".PadLeft(-1); }
            catch (Exception) { paddingRejected = true; }
            try { "value".ToCharArray(3, 5); }
            catch (Exception) { characterRangeRejected = true; }
            try { "value".IndexOf("a", 4, 2, StringComparison.Ordinal); }
            catch (Exception) { searchRangeRejected = true; }
            try { int.Parse("1x"); }
            catch (Exception) { integerParseRejected = true; }
            try { uint.Parse("-1"); }
            catch (Exception) { unsignedParseRejected = true; }
            try { bool.Parse("yes"); }
            catch (Exception) { booleanParseRejected = true; }
            try { Math.Abs(int.MinValue); }
            catch (Exception) { absoluteRejected = true; }
            try { Math.Clamp(1, 2, 1); }
            catch (Exception) { clampRejected = true; }
            try { Math.Round(1.0, 16); }
            catch (Exception) { roundRejected = true; }
            try { Math.Sign(double.NaN); }
            catch (Exception) { signRejected = true; }
            try { Guid.Parse("not-a-guid"); }
            catch (Exception) { guidParseRejected = true; }
            return Response.Json(new {
                delayRejected, substringRejected, nullSearchRejected, emptyReplacementRejected,
                removeRejected, insertRejected, paddingRejected, characterRangeRejected, searchRangeRejected,
                integerParseRejected, unsignedParseRejected, booleanParseRejected, absoluteRejected,
                clampRejected, roundRejected, signRejected, guidParseRejected
            });
        }

        if (request.Path == "/json-element-text")
        {
            var value = await request.JsonAsync<JsonElement>();
            return Response.Text(value.ToString());
        }

        if (request.Path == "/sync-iterator")
        {
            var total = 0;
            foreach (var value in Values())
                total += value;
            return Response.Json(new { total });
        }

        if (request.Path == "/response-order")
        {
            var events = new List<string>();
            var response = GetResponse(events).WithHeader(
                value: GetValue(events),
                name: GetName(events));
            return Response.Json(new { events, header = response.Headers.Get("x-order") });
        }

        if (request.Path == "/user-types")
        {
            var counter = new Counter(2) { Label = "items" };
            var person = new Person(Last: "Lovelace", First: "Ada") { Age = 36 };
            return Response.Json(new
            {
                value = counter.Add(3),
                counter.Doubled,
                counter.Label,
                person.FullName,
                greeting = person.Greet("Hello"),
                person.Age,
                counter,
                person
            });
        }

        return Response.Text("Not found", status: 404);
    }

    private static IEnumerable<int> Values()
    {
        yield return 1;
        yield return 2;
    }

    private static Response GetResponse(List<string> events)
    {
        events.Add("receiver");
        return Response.Empty();
    }

    private static string GetValue(List<string> events)
    {
        events.Add("value");
        return "set";
    }

    private static string GetName(List<string> events)
    {
        events.Add("name");
        return "x-order";
    }
}

public sealed record Parcel(string Label, int Count);

public sealed class Counter
{
    private int _value;
    public string Label { get; init; } = "counter";
    public int Doubled => _value * 2;
    public Counter(int initial) { _value = initial; }
    public int Add(int amount = 1) { _value += amount; return _value; }
}

public sealed record Person(string First, string Last)
{
    public int Age { get; init; }
    public string FullName => $"{First} {Last}";
    public string Greet(string prefix) => $"{prefix}, {FullName}";
}

public enum ParcelState
{
    Pending,
    Ready
}

public static class ParcelLimits
{
    public const int Maximum = 25;
}
