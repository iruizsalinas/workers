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

public enum ParcelState
{
    Pending,
    Ready
}

public static class ParcelLimits
{
    public const int Maximum = 25;
}
