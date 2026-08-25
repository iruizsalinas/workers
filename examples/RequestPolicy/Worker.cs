using System.Text.RegularExpressions;
using Workers;

namespace RequestPolicy;

public static class Worker
{
    private const int MaxBodyBytes = 4096;

    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Path != "/v1/readings")
            return Error("Not found", 404);

        var methods = new HashSet<string> { "POST" };
        if (!methods.Contains(request.Method))
            return Error("Method not allowed", 405).WithHeader("allow", "POST");

        if (!HasJsonBody(request))
            return Error("A bounded JSON body is required", 400);

        ReadingInput? input;
        try
        {
            input = await request.JsonAsync<ReadingInput>();
        }
        catch (Exception)
        {
            return Error("Malformed JSON", 400);
        }

        if (!IsValid(input))
            return Error("Invalid reading", 400);

        var upstream = new Url(environment.Variable("ORIGIN"));
        upstream.Path = "/v1/readings";

        var headers = request.Headers.Clone();
        headers.Delete("authorization");
        headers.Delete("cookie");
        headers.Set("content-type", "application/json");
        headers.Set("x-request-id", Guid.NewGuid().ToString());

        var controller = new AbortController();
        var timeout = Timers.SetTimeout(
            () => controller.Abort("Upstream timeout"),
            TimeSpan.FromMilliseconds(3000));
        try
        {
            var response = await Http.FetchAsync(upstream.ToString(), new FetchOptions
            {
                Method = "POST",
                Headers = headers,
                Body = Body.Json(input),
                Redirect = RedirectMode.Manual,
                Signal = controller.Signal
            });
            return response.Clone()
                .WithHeader("x-policy", "validated")
                .WithHeader("x-upstream-host", upstream.Hostname);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Error("Upstream unavailable", 502);
        }
        finally
        {
            Timers.ClearTimeout(timeout);
        }
    }

    private static bool HasJsonBody(Request request)
    {
        var contentType = request.Headers.Get("content-type");
        var contentLength = request.Headers.Get("content-length");
        if (contentType is null || !contentType.StartsWith("application/json") || contentLength is null)
            return false;
        if (!Regex.IsMatch(contentLength, "^[1-9][0-9]{0,3}$"))
            return false;
        return int.Parse(contentLength) <= MaxBodyBytes && request.BodyStream() is not null && !request.BodyUsed;
    }

    private static bool IsValid(ReadingInput? input)
    {
        if (input is null || !Regex.IsMatch(input.DeviceId, "^[a-z0-9-]{3,32}$"))
            return false;
        if (input.Value < -100 || input.Value > 1000 || input.Tags.Count > 8)
            return false;

        var tags = new HashSet<string>();
        foreach (var tag in input.Tags)
            if (tag.Length == 0 || tag.Length > 24 || !tags.Add(tag))
                return false;
        return true;
    }

    private static Response Error(string message, int status) =>
        Response.Json(new { error = message, requestId = Guid.NewGuid() }, status);
}

public sealed record ReadingInput(string DeviceId, double Value, IReadOnlyList<string> Tags);
