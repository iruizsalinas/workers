using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, Context context)
    {
        if (request.Path == "/json")
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
        if (request.Path == "/typed-json")
        {
            var input = await request.JsonAsync<User>();
            await env.Kv("KV").PutJsonAsync("native-user", input);
            var stored = await env.Kv("KV").GetJsonAsync<User>("native-user");
            var parsed = Json.Parse<User>(Json.Stringify(stored))!;
            return Response.Json(new { name = parsed.DisplayName(), serialized = Json.Stringify(parsed), user = parsed });
        }
        if (request.Path == "/typed-values")
            return Response.Json(await request.JsonAsync<TypedValues>());
        if (request.Path == "/date")
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
        if (request.Path == "/regex")
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
        if (request.Path == "/abort")
        {
            var controller = new AbortController();
            controller.Abort("stopped");
            var threw = false;
            try { controller.Signal.ThrowIfAborted(); }
            catch (Exception) { threw = true; }
            var waitRejected = false;
            try { await Timers.WaitAsync(10, controller.Signal); }
            catch (Exception) { waitRejected = true; }
            var combined = AbortSignal.Any([controller.Signal, request.Signal]);
            return Response.Json(new { controller.Signal.Aborted, controller.Signal.Reason, threw, waitRejected, combined = combined.Aborted });
        }
        if (request.Path == "/text")
        {
            var bytes = TextCodec.EncodeUtf8("hello 😀");
            return Response.Json(new
            {
                decoded = TextCodec.DecodeUtf8(bytes),
                base64 = TextCodec.EncodeBase64(bytes),
                fromBase64 = TextCodec.DecodeUtf8(TextCodec.DecodeBase64(TextCodec.EncodeBase64(bytes))),
                hex = TextCodec.EncodeHex(bytes),
                fromHex = TextCodec.DecodeUtf8(TextCodec.DecodeHex(TextCodec.EncodeHex(bytes))),
                encoded = Url.EncodeComponent("a b!*"), decodedUrl = Url.DecodeComponent("a%20b"),
                uuid = Crypto.RandomUuid()
            });
        }
        if (request.Path == "/numbers")
            return Response.Json(new
            {
                rounded = Numbers.Round(2.5), negative = Numbers.Round(-1.5),
                fixedText = Numbers.ToFixed(2.675, 2), precision = Numbers.ToPrecision(12.345, 4),
                radix = Numbers.ToRadixString(255, 16), partialInt = Numbers.ParseInt("12px"),
                partialFloat = Numbers.ParseFloat("1.25px"), nan = Numbers.IsNaN(Numbers.ParseFloat("invalid")),
                nullNumber = Numbers.From(null), safe = Numbers.IsSafeInteger(9007199254740991d),
                invalidSign = Numbers.Sign(double.NaN), random = Numbers.Random()
            });
        if (request.Path == "/foundation")
        {
            var numbers = new List<int> { 3, 1, 2, 2 };
            var sorted = numbers.Distinct().OrderBy(number => number).ToArray();
            var dictionary = new Dictionary<string, int> { ["__proto__"] = 7 };
            var tasks = await Task.WhenAll(ComputeAsync(3), ComputeAsync(4));
            var queue = new Queue<int>();
            queue.Enqueue(10);
            return Response.Json(new { sorted, sum = numbers.Sum(), specialKey = dictionary["__proto__"], tasks, queued = queue.Dequeue() });
        }
        return Response.Text("Not found", 404);
    }

    private static async Task<int> ComputeAsync(int value)
    {
        await Timers.WaitAsync(1);
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
