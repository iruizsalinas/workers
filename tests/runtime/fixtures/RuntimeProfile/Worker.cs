using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
    {
        if (new Url(request.Url).Pathname == "/json")
        {
            var value = Json.Parse(await request.TextAsync());
            return Response.Json(new
            {
                name = value["name"].AsString(),
                number = value["number"].AsNumber(),
                enabled = value["enabled"].AsBoolean(),
                explicitNull = value["empty"].IsNull,
                missing = value["missing"].IsUndefined,
                hasMissing = value.Has("missing"),
                count = value["items"].Length,
                serialized = Json.Stringify(value)
            });
        }
        if (new Url(request.Url).Pathname == "/typed-json")
        {
            var input = await request.JsonAsync<User>();
            await env.KV("KV").PutJsonAsync("native-user", input);
            var stored = await env.KV("KV").GetJsonAsync<User>("native-user");
            var parsed = Json.Parse<User>(Json.Stringify(stored))!;
            return Response.Json(new { name = parsed.DisplayName(), serialized = Json.Stringify(parsed), user = parsed });
        }
        if (new Url(request.Url).Pathname == "/typed-values")
            return Response.Json(await request.JsonAsync<TypedValues>());
        if (new Url(request.Url).Pathname == "/date")
        {
            var date = new Date(Date.Utc(2024, 1, 29, 23, 58, 57, 123));
            var original = date.ToIsoString();
            date.SetUtcMonth(2);
            return Response.Json(new
            {
                original, date = date.ToIsoString(), year = date.GetUtcFullYear(), month = date.GetUtcMonth(),
                day = date.GetUtcDate(), timestamp = date.GetTime(),
                parsed = Date.Parse(original), invalid = new Date("invalid").GetTime(),
                epoch = new Date(0).ToIsoString()
            });
        }
        if (new Url(request.Url).Pathname == "/regex")
        {
            var expression = new RegExp("(?<word>[a-z]+)", "g");
            var first = expression.Exec("one two")!;
            var nextIndex = expression.LastIndex;
            var second = expression.Exec("one two")!;
            expression.LastIndex = 0;
            return Response.Json(new
            {
                first = first[0], group = first.Groups!["word"], first.Index, nextIndex,
                second = second[0], expression.Source, expression.Flags,
                replaced = expression.Replace("one two", "$<word>!"),
                split = new RegExp("[,;]").Split("a,b;c"),
                unicode = new RegExp("^.$", "u").Test("😀"),
                noMatch = expression.Exec("123") is null
            });
        }
        if (new Url(request.Url).Pathname == "/abort")
        {
            var controller = new AbortController();
            controller.Abort("stopped");
            var threw = false;
            try { controller.Signal.ThrowIfAborted(); }
            catch (Exception) { threw = true; }
            var waitRejected = false;
            try { await Scheduler.WaitAsync(10, controller.Signal); }
            catch (Exception) { waitRejected = true; }
            var combined = AbortSignal.Any([controller.Signal, request.Signal]);
            return Response.Json(new { controller.Signal.Aborted, controller.Signal.Reason, threw, waitRejected, combined = combined.Aborted });
        }
        if (new Url(request.Url).Pathname == "/text")
        {
            var bytes = new TextEncoder().Encode("hello 😀");
            return Response.Json(new
            {
                decoded = new TextDecoder().Decode(bytes),
                base64 = Base64.Encode(bytes),
                fromBase64 = new TextDecoder().Decode(Base64.Decode(Base64.Encode(bytes))),
                hex = Hex.Encode(bytes),
                fromHex = new TextDecoder().Decode(Hex.Decode(Hex.Encode(bytes))),
                encoded = Url.EncodeComponent("a b!*"), decodedUrl = Url.DecodeComponent("a%20b"),
                uuid = Crypto.RandomUuid()
            });
        }
        if (new Url(request.Url).Pathname == "/numbers")
            return Response.Json(new
            {
                rounded = Numbers.Round(2.5), negative = Numbers.Round(-1.5),
                fixedText = Numbers.ToFixed(2.675, 2), precision = Numbers.ToPrecision(12.345, 4),
                radix = Numbers.ToRadixString(255, 16), partialInt = Numbers.ParseInt("12px"),
                partialFloat = Numbers.ParseFloat("1.25px"), nan = Numbers.IsNaN(Numbers.ParseFloat("invalid")),
                nullNumber = Numbers.From(null), safe = Numbers.IsSafeInteger(9007199254740991d),
                invalidSign = Numbers.Sign(double.NaN), random = Numbers.Random()
            });
        if (new Url(request.Url).Pathname == "/foundation")
        {
            var numbers = new List<int> { 3, 1, 2, 2 };
            var sorted = numbers.Distinct().OrderBy(number => number).ToArray();
            var dictionary = new Dictionary<string, int> { ["__proto__"] = 7 };
            var tasks = await Task.WhenAll(ComputeAsync(3), ComputeAsync(4));
            var queue = new Queue<int>();
            queue.Enqueue(10);
            return Response.Json(new { sorted, sum = numbers.Sum(), specialKey = dictionary["__proto__"], tasks, queued = queue.Dequeue() });
        }
        return new Response("Not found") { Status = 404 };
    }

    private static async Task<int> ComputeAsync(int value)
    {
        await Scheduler.WaitAsync(1);
        return value * value;
    }
}

public sealed record User([property: JsonName("user_name")] string Name, int Count)
{
    [JsonIgnore]
    public string Secret { get; init; } = "hidden";
    public string DisplayName() => $"{Name}:{Count}";
}

public sealed record TypedValues(bool Enabled, double Count, Date At, TypedValues[] Children);
