using System.Text.Json;
using System.Text.Json.Nodes;

// System.Text.Json.Nodes compiled both by the CLR and by Workers: parsing, building and patching
// documents, the single-parent rule, typed reads, equality, cloning, escaping and serializer interop.
public static class JsonNodeScenarios
{
    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        results["parsing"] = Parsing();
        results["building"] = Building();
        results["indented"] = Indented();
        results["objects"] = Objects();
        results["arrays"] = Arrays();
        results["parents"] = Parents();
        results["values"] = Values();
        results["conversions"] = Conversions();
        results["kinds"] = Kinds();
        results["navigation"] = Navigation();
        results["equality"] = Equality();
        results["cloning"] = Cloning();
        results["patterns"] = Patterns();
        results["enumeration"] = Enumeration();
        results["tryMethods"] = TryMethods();
        results["serializer"] = Serializer();
        results["escaping"] = Escaping();
        results["specialKeys"] = SpecialKeys();
        results["depth"] = Depth();
        results["largeNumbers"] = LargeNumbers();
        results["created"] = Created();
        results["text"] = Text();
        results["patching"] = Patching("{\"id\":12345678901234567890,\"user\":{\"name\":\"Ada\",\"roles\":[\"admin\"]},\"tags\":[\"a\",\"b\"]}");
        return results;
    }

    // Delegates are invoked through LINQ, which the Workers profile supports.
    private static string Attempt(Func<int, string> action)
    {
        try
        {
            return new[] { 0 }.Select(action).First();
        }
        catch (Exception)
        {
            return "error";
        }
    }

    private static string Parsing()
    {
        var inputs = new[] { "{\"a\":1,\"b\":[true,false,null],\"c\":{\"d\":\"e\"}}", "[1, 2.5, -3, 0]", "\"text\"", "42", "true",
            "  [1]  ", "{} x", "/*c*/{}", "[1,]", "", "NaN", "{\"a\":}", "[\"\\u00e9\"]" };
        return string.Join(" | ", inputs.Select(json => Attempt(_ => JsonNode.Parse(json)?.ToJsonString() ?? "null")))
            + " | " + Attempt(_ => JsonNode.Parse("null") is null ? "null" : "node")
            + " " + Attempt(_ => JsonNode.Parse((string)null!)!.ToJsonString());
    }

    private static string Building()
    {
        var document = new JsonObject
        {
            ["name"] = "widget",
            ["count"] = 3,
            ["price"] = 1.5,
            ["ratio"] = 1.1f,
            ["active"] = true,
            ["missing"] = null,
            ["sizes"] = new JsonArray(1, 2, 3),
            ["nested"] = new JsonObject { { "x", 1 }, { "y", "two" } }
        };
        document["count"] = 4;
        document["extra"] = new JsonArray { 1, "x", null, new JsonObject() };
        JsonArray listed = [1, "two", 3.25];
        JsonNode number = 7;
        JsonNode? text = "seven";
        return $"{document.ToJsonString()} {listed.ToJsonString()} {number.ToJsonString()} {text!.ToJsonString()} {document.Count}"
            + $" {Attempt(_ => new JsonObject { { "a", 1 }, { "a", 2 } }.ToJsonString())} {new JsonObject { ["a"] = 1, ["a"] = 2 }.ToJsonString()}"
            + $" {new JsonArray(new JsonNode?[] { 1, null }).ToJsonString()}";
    }

    private static string Indented()
    {
        var document = JsonNode.Parse("{\"a\":[1,[2,{\"b\":[]}],{}],\"c\":\"<\\u00e9>\",\"d\":null}")!;
        // The CLR indents with Environment.NewLine; Workers always writes \n, as the CLR does on Linux.
        return (document.ToString() + "|" + new JsonArray().ToString() + "|" + new JsonObject().ToString() + "|" + JsonNode.Parse("[[]]")!.ToString())
            .Replace("\r\n", "\n");
    }

    private static string Objects()
    {
        var target = new JsonObject { ["a"] = 1, ["b"] = 2 };
        var added = target.TryAdd("c", 3);
        var duplicate = target.TryAdd("a", 9);
        var removed = target.Remove("b");
        var missing = target.Remove("zzz");
        target.Add("d", "four");
        target.Add(new KeyValuePair<string, JsonNode?>("e", 5));
        var before = target.ToJsonString();
        var contains = target.ContainsKey("d") && !target.ContainsKey("b");
        var count = target.Count;
        target.Clear();
        return $"{added} {duplicate} {removed} {missing} {before} {contains} {count} {target.Count} {target.ToJsonString()}"
            + $" {Attempt(_ => { var value = new JsonObject(); value.Add("x", 1); value.Add("x", 2); return "ok"; })}"
            + $" {Attempt(_ => { var value = new JsonObject(); value.Add(null!, 1); return "ok"; })}"
            + $" {Attempt(_ => { var value = new JsonObject(); value[null!] = 1; return "ok"; })}"
            + $" {Attempt(_ => new JsonObject()[null!]?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => new JsonObject().ContainsKey(null!).ToString())}"
            + $" {new JsonObject(new[] { KeyValuePair.Create<string, JsonNode?>("k", 1) }).ToJsonString()}";
    }

    private static string Arrays()
    {
        var items = new JsonArray(1, 2, 3);
        items.Add(4);
        items.Add("five");
        items.Add<JsonNode?>(null);
        items.Insert(0, 0);
        items.Insert(items.Count, "end");
        items[1] = "one";
        var afterInsert = items.ToJsonString();
        items.RemoveAt(0);
        items.RemoveRange(1, 2);
        var removedAll = items.RemoveAll(item => item is JsonValue && item.GetValueKind() == JsonValueKind.String);
        var afterRemove = items.ToJsonString();
        var numbers = new JsonArray(5, 6, 7);
        var values = string.Join(",", numbers.GetValues<int>().Select(value => value.ToString()));
        items.Clear();
        return $"{afterInsert} {afterRemove} {removedAll} {items.Count} {values} {numbers[2]!.ToJsonString()}"
            + $" {Attempt(_ => new JsonArray(1, 2)[5]!.ToJsonString())} {Attempt(_ => new JsonArray(1, 2)[-1]!.ToJsonString())}"
            + $" {Attempt(_ => { var value = new JsonArray(1); value[1] = 2; return "ok"; })}"
            + $" {Attempt(_ => { var value = new JsonArray(1, 2, 3); value.Insert(4, 9); return "ok"; })}"
            + $" {Attempt(_ => { var value = new JsonArray(1, 2); value.RemoveRange(1, 5); return "ok"; })}"
            + $" {Attempt(_ => { var value = new JsonArray(1, 2); value.RemoveAt(2); return "ok"; })}"
            + $" {Attempt(_ => string.Join(",", new JsonArray(1, "x").GetValues<int>().Select(value => value.ToString())))}";
    }

    private static string Parents()
    {
        return string.Join(" ", new[]
        {
            Attempt(_ => { var a = new JsonObject(); var b = new JsonObject(); var c = new JsonObject(); a["x"] = c; b["y"] = c; return "ok"; }),
            Attempt(_ => { var a = new JsonObject(); a["self"] = a; return "ok"; }),
            Attempt(_ => { var a = new JsonObject(); var b = new JsonObject(); a["b"] = b; b["a"] = a; return "ok"; }),
            Attempt(_ => { var a = new JsonArray(); var b = new JsonArray(); a.Add(b); b.Add(a); return "ok"; }),
            Attempt(_ => { var a = new JsonObject(); var c = new JsonObject(); a["x"] = c; a["x"] = c; return a.ToJsonString(); }),
            Attempt(_ => { var a = new JsonObject(); var b = new JsonObject(); var c = new JsonObject(); a["x"] = c; a.Remove("x"); b["y"] = c; return b.ToJsonString(); }),
            Attempt(_ => { var a = new JsonObject(); var b = new JsonObject(); var c = new JsonObject(); a["x"] = c; a["x"] = 1; b["y"] = c; return b.ToJsonString(); }),
            Attempt(_ => { var a = new JsonArray(); var b = new JsonArray(); var c = new JsonObject(); a.Add(c); a.Clear(); b.Add(c); return b.ToJsonString(); }),
            Attempt(_ => { var a = new JsonArray(); var b = new JsonArray(); var c = new JsonArray(); a.Add(c); a.RemoveAt(0); b.Add(c); return b.ToJsonString(); }),
            Attempt(_ => { var a = new JsonArray(); var c = new JsonArray(); a.Add(c); a[0] = null; var b = new JsonObject { ["c"] = c }; return b.ToJsonString(); }),
            Attempt(_ => { var c = new JsonObject(); var a = new JsonArray(c, c); return a.ToJsonString(); }),
            Attempt(_ => { var parsed = JsonNode.Parse("{\"inner\":{\"v\":1}}")!; var other = new JsonObject { ["moved"] = parsed["inner"] }; return other.ToJsonString(); }),
            Attempt(_ => { var parsed = JsonNode.Parse("{\"inner\":{\"v\":1}}")!; var inner = parsed["inner"]!; parsed.AsObject().Remove("inner"); return new JsonObject { ["moved"] = inner }.ToJsonString(); }),
            Attempt(_ => { var a = new JsonObject(); var c = new JsonObject(); a["x"] = c; return new JsonObject { ["y"] = c.DeepClone() }.ToJsonString(); })
        });
    }

    private static string Values()
    {
        var document = JsonNode.Parse("{\"i\":5,\"f\":1.5,\"neg\":-1,\"big\":300,\"s\":\"h\",\"long\":\"ab\",\"t\":true,\"n\":null,"
            + "\"g\":\"6F9619FF-8B86-D011-B42D-00CF4FC964FF\",\"d\":\"2026-10-08T01:02:03Z\",\"o\":{},\"maxInt\":2147483648}")!;
        return string.Join(" ", new[]
        {
            Attempt(_ => document["i"]!.GetValue<int>().ToString()),
            Attempt(_ => document["i"]!.GetValue<double>().ToString()),
            Attempt(_ => document["f"]!.GetValue<int>().ToString()),
            Attempt(_ => document["f"]!.GetValue<double>().ToString()),
            Attempt(_ => document["f"]!.GetValue<float>().ToString()),
            Attempt(_ => document["neg"]!.GetValue<uint>().ToString()),
            Attempt(_ => document["big"]!.GetValue<byte>().ToString()),
            Attempt(_ => document["big"]!.GetValue<short>().ToString()),
            Attempt(_ => document["maxInt"]!.GetValue<int>().ToString()),
            Attempt(_ => document["maxInt"]!.GetValue<long>().ToString()),
            Attempt(_ => document["s"]!.GetValue<string>()),
            Attempt(_ => document["s"]!.GetValue<char>().ToString()),
            Attempt(_ => document["long"]!.GetValue<char>().ToString()),
            Attempt(_ => document["s"]!.GetValue<int>().ToString()),
            Attempt(_ => document["i"]!.GetValue<string>()),
            Attempt(_ => document["t"]!.GetValue<bool>().ToString()),
            Attempt(_ => document["s"]!.GetValue<bool>().ToString()),
            Attempt(_ => document["g"]!.GetValue<Guid>().ToString()),
            Attempt(_ => document["s"]!.GetValue<Guid>().ToString()),
            Attempt(_ => document["d"]!.GetValue<DateTimeOffset>().ToUnixTimeSeconds().ToString()),
            Attempt(_ => document["o"]!.GetValue<int>().ToString()),
            Attempt(_ => document["n"]?.GetValue<int>().ToString() ?? "null"),
            Attempt(_ => document["i"]!.GetValue<JsonElement>().GetInt32().ToString())
        });
    }

    private static string Conversions()
    {
        JsonNode? number = JsonNode.Parse("12");
        JsonNode? text = JsonNode.Parse("\"hi\"");
        JsonNode? missing = null;
        return $"{(int)number!} {(double)number!} {(long)number!} {(string?)text} {(int?)missing is null} {(string?)missing is null}"
            + $" {(int?)number} {(bool)JsonNode.Parse("false")!} {(float)JsonNode.Parse("0.5")!}"
            + $" {Attempt(_ => ((int)missing!).ToString())} {Attempt(_ => ((string?)number) ?? "null")} {Attempt(_ => ((int)text!).ToString())}"
            + $" {Attempt(_ => ((JsonObject)number!).Count.ToString())} {Attempt(_ => ((JsonArray)JsonNode.Parse("[1]")!).Count.ToString())}";
    }

    private static string Kinds()
    {
        var nodes = JsonNode.Parse("[1,\"a\",true,false,{},[],1.5]")!.AsArray();
        return string.Join(",", nodes.Select(node => ((int)node!.GetValueKind()).ToString()))
            + $" {(int)new JsonObject().GetValueKind()} {(int)JsonValue.Create(5).GetValueKind()} {(int)JsonValue.Create("x")!.GetValueKind()}"
            + $" {Attempt(_ => nodes.AsObject().Count.ToString())} {Attempt(_ => nodes[0]!.AsArray().Count.ToString())}"
            + $" {Attempt(_ => nodes[4]!.AsValue().ToJsonString())} {Attempt(_ => nodes[0]!.AsValue().ToJsonString())}"
            + $" {Attempt(_ => nodes[5]!.AsObject().Count.ToString())} {nodes[4]!.AsObject().Count} {nodes[5]!.AsArray().Count}";
    }

    private static string Navigation()
    {
        JsonNode document = JsonNode.Parse("{\"user\":{\"name\":\"Ada\",\"langs\":[\"en\",\"fr\"]},\"n\":1}")!;
        document["user"]!["age"] = 36;
        document["user"]!["langs"]![1] = "de";
        JsonNode list = new JsonArray(1, 2);
        list[0] = "first";
        return $"{document["user"]!["name"]} {document["user"]!["langs"]![1]} {document["user"]!["age"]} {document["nope"] is null}"
            + $" {document[0]!.ToJsonString()} {document["user"]?["missing"]?["deeper"] is null} {list.ToJsonString()}"
            + $" {document.ToJsonString()}"
            + $" {Attempt(_ => list["x"]?.ToJsonString() ?? "null")} {Attempt(_ => document[5]?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => document["n"]!["x"]?.ToJsonString() ?? "null")} {Attempt(_ => document["n"]![0]?.ToJsonString() ?? "null")}";
    }

    private static string Equality()
    {
        return string.Join(" ", new[]
        {
            JsonNode.DeepEquals(JsonNode.Parse("1"), JsonNode.Parse("1.0")),
            JsonNode.DeepEquals(JsonNode.Parse("1"), JsonValue.Create(1)),
            JsonNode.DeepEquals(JsonNode.Parse("100"), JsonNode.Parse("1e2")),
            JsonNode.DeepEquals(JsonNode.Parse("0.1"), JsonNode.Parse("0.10000000000000001")),
            JsonNode.DeepEquals(JsonNode.Parse("{\"a\":1,\"b\":[1,{\"c\":null}]}"), JsonNode.Parse("{\"b\":[1,{\"c\":null}],\"a\":1}")),
            JsonNode.DeepEquals(JsonNode.Parse("{\"a\":1}"), JsonNode.Parse("{\"a\":1,\"b\":2}")),
            JsonNode.DeepEquals(JsonNode.Parse("[1,2]"), JsonNode.Parse("[2,1]")),
            JsonNode.DeepEquals(null, null),
            JsonNode.DeepEquals(null, JsonNode.Parse("null")),
            JsonNode.DeepEquals(JsonValue.Create("1"), JsonValue.Create(1)),
            JsonNode.DeepEquals(new JsonObject(), new JsonArray()),
            JsonNode.DeepEquals(JsonValue.Create(true), JsonNode.Parse("true")),
            JsonNode.DeepEquals(JsonNode.Parse("12345678901234567890"), JsonNode.Parse("12345678901234567891"))
        }.Select(value => value.ToString()));
    }

    private static string Cloning()
    {
        var source = JsonNode.Parse("{\"a\":[1,{\"b\":2}],\"c\":\"d\"}")!;
        var copy = source.DeepClone();
        copy["a"]![1]!["b"] = 3;
        copy["e"] = new JsonArray(source["a"]!.DeepClone());
        return $"{source.ToJsonString()} {copy.ToJsonString()} {JsonNode.DeepEquals(source, source.DeepClone())}"
            + $" {JsonValue.Create("x").DeepClone().ToJsonString()}";
    }

    private static string Describe(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject value => $"object:{value.Count}",
        JsonArray value => $"array:{value.Count}",
        JsonValue value when value.GetValueKind() == JsonValueKind.String => $"string:{value.GetValue<string>()}",
        _ => $"value:{node.ToJsonString()}"
    };

    private static string Patterns()
    {
        var nodes = JsonNode.Parse("[{\"a\":1},[1,2,3],\"s\",4,null,true]")!.AsArray();
        var described = string.Join(",", nodes.Select(Describe));
        var node = nodes[0];
        var asObject = node as JsonObject;
        var asArray = node as JsonArray;
        var asValue = nodes[3] as JsonValue;
        var checks = $"{node is JsonObject} {node is JsonArray} {node is JsonValue} {node is JsonNode} {nodes[4] is JsonNode}"
            + $" {asObject?.Count} {asArray is null} {asValue?.ToJsonString()}";
        if (nodes[1] is JsonArray list && list.Count == 3) checks += " list";
        return described + " " + checks + " " + Describe(5) + " " + Describe("five");
    }

    private static string Enumeration()
    {
        var document = JsonNode.Parse("{\"b\":1,\"a\":[1,2,3],\"c\":{\"d\":true}}")!.AsObject();
        var keys = new List<string>();
        foreach (var pair in document) keys.Add($"{pair.Key}={pair.Value?.ToJsonString()}");
        var total = 0;
        foreach (var item in document["a"]!.AsArray()) total += item!.GetValue<int>();
        var linq = document["a"]!.AsArray().Where(item => item!.GetValue<int>() > 1).Select(item => (item!.GetValue<int>() * 10).ToString());
        var names = document.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal);
        return $"{string.Join(",", keys)} {total} {string.Join(",", linq)} {string.Join(",", names)}"
            + $" {document["a"]!.AsArray().Count(item => item!.GetValue<int>() >= 2)} {document.Any(pair => pair.Value is JsonObject)}"
            + $" {new JsonArray(document["a"]!.AsArray().Select(item => (JsonNode?)(item!.GetValue<int>() + 1)).ToArray()).ToJsonString()}";
    }

    private static string TryMethods()
    {
        var document = JsonNode.Parse("{\"a\":\"x\",\"n\":null,\"i\":4}")!.AsObject();
        var found = document.TryGetPropertyValue("a", out var a);
        var nullFound = document.TryGetPropertyValue("n", out var n);
        var missing = document.TryGetPropertyValue("zzz", out var z);
        var number = document["i"]!.AsValue().TryGetValue<int>(out var i);
        var text = document["a"]!.AsValue().TryGetValue<int>(out var notNumber);
        var parsedText = document["a"]!.AsValue().TryGetValue<string>(out var s);
        return $"{found} {a?.ToJsonString()} {nullFound} {n is null} {missing} {z is null} {number} {i} {text} {notNumber} {parsedText} {s}"
            + $" {Attempt(_ => document.TryGetPropertyValue(null!, out JsonNode? _).ToString())}";
    }

    public sealed record Item(string Name, int Count, double Weight, DateOnly Day, List<string> Tags);

    private static string Serializer()
    {
        var item = new Item("bolt", 3, 0.5, new DateOnly(2026, 1, 2), ["a", "b"]);
        var node = JsonSerializer.SerializeToNode(item)!;
        node["Count"] = 4;
        var back = JsonSerializer.Deserialize<Item>(node)!;
        var viaExtension = node.Deserialize<Item>()!;
        var parsed = JsonSerializer.Deserialize<JsonObject>("{\"k\":[1,2]}")!;
        var bytes = JsonSerializer.Deserialize<JsonNode>(JsonSerializer.SerializeToUtf8Bytes(new { x = 1 }))!;
        var anonymous = JsonSerializer.Serialize(new { node = (JsonNode)new JsonObject { ["a"] = 1.5 }, list = new List<JsonNode?> { 1, "x", null } });
        return $"{node.ToJsonString()} {back.Count} {back.Day:O} {string.Join("+", back.Tags)} {viaExtension.Name} {parsed.ToJsonString()}"
            + $" {bytes.ToJsonString()} {anonymous} {JsonSerializer.Serialize<JsonNode>(parsed)} {JsonSerializer.Serialize<JsonNode?>(null)}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonObject>("5")?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonArray>("{}")?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonValue>("{}")?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonValue>("null")?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonObject>("null")?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<Item>(JsonNode.Parse("{\"Name\":5}"))?.Name ?? "null")}"
            + $" {JsonObject.Create(JsonSerializer.Deserialize<JsonElement>("{\"e\":[1]}"))!.ToJsonString()}"
            + $" {JsonArray.Create(JsonSerializer.Deserialize<JsonElement>("[1,{}]"))!.ToJsonString()}"
            + $" {JsonValue.Create(JsonSerializer.Deserialize<JsonElement>("\"v\""))!.ToJsonString()}"
            + $" {Attempt(_ => JsonObject.Create(JsonSerializer.Deserialize<JsonElement>("[1]"))?.ToJsonString() ?? "null")}"
            + $" {Attempt(_ => JsonValue.Create(JsonSerializer.Deserialize<JsonElement>("{}"))?.ToJsonString() ?? "null")}";
    }

    private static string Escaping()
    {
        var document = new JsonObject
        {
            ["\u00e9<key>"] = "\"\\/\b\f\n\r\t\u0001\u001f'+`&<>~\u00e9\ud83d\ude00\u007f",
            ["lone"] = "a\ud800b",
            ["quote\"key"] = "\u2028"
        };
        return document.ToJsonString() + " " + JsonSerializer.Serialize(document) + " " + JsonSerializer.Serialize(new Dictionary<string, int> { ["\u00e9"] = 1 })
            + " " + document["lone"]!.GetValue<string>().Length;
    }

    private static string SpecialKeys()
    {
        var document = JsonNode.Parse("{\"__proto__\":{\"x\":1},\"constructor\":2,\"toString\":3}")!.AsObject();
        document["hasOwnProperty"] = 4;
        document["__proto__"]!["y"] = 5;
        var created = new JsonObject { ["__proto__"] = "p", ["valueOf"] = null };
        return $"{document.ToJsonString()} {document.Count} {document["constructor"]} {document.ContainsKey("toString")}"
            + $" {document["missing"] is null} {new JsonObject()["constructor"] is null} {new JsonObject().ContainsKey("__proto__")}"
            + $" {created.ToJsonString()} {created.Count} {created.Remove("__proto__")} {created.ToJsonString()}"
            + $" {string.Join(",", document.Select(pair => pair.Key))}";
    }

    private static string Nested(int depth) => "".PadLeft(depth, '[') + "".PadLeft(depth, ']');

    private static string Objects(int depth) => "".PadLeft(depth, '#').Replace("#", "{\"a\":") + "1" + "".PadLeft(depth, '}');

    private static string Depth()
    {
        return $"{Attempt(_ => JsonNode.Parse(Nested(64))!.ToJsonString().Length.ToString())}"
            + $" {Attempt(_ => JsonNode.Parse(Nested(65))!.ToJsonString().Length.ToString())}"
            + $" {Attempt(_ => JsonNode.Parse(Objects(64))!.ToJsonString().Length.ToString())}"
            + $" {Attempt(_ => JsonNode.Parse(Objects(65))!.ToJsonString().Length.ToString())}"
            + $" {Attempt(_ => JsonSerializer.Deserialize<JsonNode>(Nested(65))!.ToJsonString())}";
    }

    private static string LargeNumbers()
    {
        var document = JsonNode.Parse("{\"id\":12345678901234567890,\"precise\":0.10000000000000000001,\"huge\":1E400,\"safe\":9007199254740993}")!;
        document["note"] = "patched";
        return $"{document.ToJsonString()} {document["id"]!.GetValue<double>()} {(int)document["id"]!.GetValueKind()}"
            + $" {Attempt(_ => document["id"]!.GetValue<int>().ToString())} {Attempt(_ => (document["huge"]!.GetValue<double>() > 1e308).ToString())}"
            + $" {document["precise"]!.GetValue<double>()} {document["id"]!.ToString()} {document.DeepClone()["id"]!.ToJsonString()}";
    }

    private static string Created()
    {
        var values = new JsonArray
        {
            1e21, 1e-7, 0.1, 123456789012.5, -0.0, 5.0, 1.1f, 16777217f, 9007199254740991L,
            JsonValue.Create(Guid.Empty), JsonValue.Create('c'), JsonValue.Create(new DateOnly(2026, 1, 2)),
            JsonValue.Create(new TimeOnly(8, 30)), JsonValue.Create((string?)null), JsonValue.Create((int?)null), JsonValue.Create((int?)3)
        };
        values.Add(new TimeOnly(9, 15, 0, 500));
        values.Add('x');
        // .NET writes date values without escaping '+'; both forms read back as the same string.
        var dates = new JsonArray(new DateTimeOffset(2026, 10, 8, 1, 2, 3, TimeSpan.Zero), new DateTimeOffset(2026, 10, 8, 1, 2, 3, 450, TimeSpan.Zero),
            JsonValue.Create(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var dateText = string.Join(",", JsonNode.Parse(dates.ToJsonString())!.AsArray().Select(date => date!.GetValue<string>()));
        return values.ToJsonString() + " " + dateText
            + $" {Attempt(_ => { var value = new JsonObject(); value["x"] = double.NaN; return value.ToJsonString(); })}"
            + $" {Attempt(_ => new JsonArray(double.PositiveInfinity).ToJsonString())}"
            + $" {JsonValue.Create(1.1f).GetValue<float>()} {(int)dates[1]!.GetValueKind()} {dates[1]!.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds()}";
    }

    private static string Text()
    {
        var document = JsonNode.Parse("{\"s\":\"a\\\"b\",\"n\":2.5,\"b\":true,\"o\":{\"x\":[1]}}")!;
        JsonNode? missing = null;
        return ($"{document["s"]} {document["n"]} {document["b"]} [{missing}] " + "s=" + document["s"] + " o=" + document["o"]
            + $" {document["s"]!.ToJsonString()} {document["n"]!.ToString()} {JsonValue.Create(0.1 + 0.2).ToString()}").Replace("\r\n", "\n");
    }

    // The common proxy shape: read a payload, add and change fields, forward it.
    private static string Patching(string body)
    {
        var payload = JsonNode.Parse(body)!.AsObject();
        payload["forwarded"] = true;
        payload["user"]!["roles"]!.AsArray().Add("auditor");
        payload["tags"]!.AsArray().RemoveAt(0);
        payload.Remove("missing");
        var envelope = new JsonObject { ["payload"] = payload.DeepClone(), ["receivedAt"] = new DateOnly(2026, 10, 8).ToString("O") };
        return payload.ToJsonString() + " " + envelope.ToJsonString();
    }
}
