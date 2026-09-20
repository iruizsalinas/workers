namespace Workers.Compiler.Tests;

public sealed class UserTypeTests
{
    [Fact]
    public void EmitsClassesConstructorsInstanceMethodsAndComputedProperties()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var counter = new Counter(2) { Label = "items" };
                    return Response.Json(new { value = counter.Add(3), counter.Doubled, counter.Label });
                }
            }

            public sealed class Counter
            {
                private int _value;
                public string Label { get; init; } = "counter";
                public int Doubled => _value * 2;
                public Counter(int initial) { _value = initial; }
                public int Add(int amount = 1) { _value += amount; return _value; }
            }
            """);

        Assert.Contains("class $workers$Counter", module);
        Assert.Contains("constructor(initial)", module);
        Assert.Contains("this._value = 0;", module);
        Assert.Contains("this.label = \"counter\";", module);
        Assert.Contains("get doubled()", module);
        Assert.Contains("Math.imul(this._value, 2)", module);
        Assert.Contains("$workers$value.label = \"items\";", module);
        Assert.Contains("counter.$workers$cs$Counter$add(3)", module);
        Assert.Contains("return { label: this.label, doubled: this.doubled };", module);
    }

    [Fact]
    public void EmitsRecordInitializersMethodsAndComputedProperties()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var person = new Person(Last: "Lovelace", First: "Ada") { Age = 36 };
                    return Response.Json(new { person.FullName, greeting = person.Greet("Hello"), person.Age });
                }
            }

            public sealed record Person(string First, string Last)
            {
                public int Age { get; init; }
                public string FullName => $"{First} {Last}";
                public string Greet(string prefix) => $"{prefix}, {FullName}";
            }
            """);

        Assert.Contains("class $workers$Person", module);
        Assert.Contains("=> new $workers$Person($workers$arg2, $workers$arg1)", module);
        Assert.Contains("this.first = First;", module);
        Assert.Contains("this.last = Last;", module);
        Assert.Contains("$workers$value.age = 36;", module);
        Assert.Contains("get fullName()", module);
        Assert.Contains("person.$workers$cs$Person$greet(\"Hello\")", module);
        Assert.Contains("first: this.first", module);
        Assert.Contains("fullName: this.fullName", module);
    }

    [Theory]
    [InlineData("public class Child : Base { } public class Base { }")]
    [InlineData("public class Box<T> { }")]
    [InlineData("public partial class Split { } public partial class Split { }")]
    [InlineData("public class Overloaded { public Overloaded() { } public Overloaded(int value) { } }")]
    [InlineData("public class Indexed { public int this[int index] => index; }")]
    [InlineData("public class Mutable { private int _value; public int Value { get => _value; set => _value = value; } }")]
    public void RejectsUnsupportedUserTypeShapes(string declaration)
    {
        var typeName = declaration.Contains("Child", StringComparison.Ordinal) ? "Child"
            : declaration.Contains("Box", StringComparison.Ordinal) ? "Box<int>"
            : declaration.Contains("Split", StringComparison.Ordinal) ? "Split"
            : declaration.Contains("Overloaded", StringComparison.Ordinal) ? "Overloaded"
            : declaration.Contains("Indexed", StringComparison.Ordinal) ? "Indexed"
            : "Mutable";
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new {{typeName}}());
            }
            {{declaration}}
            """));

        Assert.StartsWith("WRK119:", error.Message);
    }

    [Fact]
    public void KeepsNestedDataOnlyRecordsAsStructuredCloneableObjects()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new Container.Item("nested"));
            }
            public static class Container
            {
                public sealed record Item(string Value);
            }
            """);

        Assert.Contains("{ value: \"nested\" }", module);
        Assert.DoesNotContain("class $workers$Item", module);
    }

    [Theory]
    [InlineData("public int Constructor => 1;")]
    [InlineData("public string ToJSON { get; init; } = \"x\";")]
    public void RejectsPropertiesThatCollideWithJavascriptClassBehavior(string property)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new Model());
            }
            public class Model { {{property}} }
            """));

        Assert.StartsWith("WRK119:", error.Message);
    }

    [Fact]
    public void KeepsFieldsAndCaseDistinctPropertiesCollisionSafe()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var counter = new Counter();
                    var pair = new Pair { Value = 3, value = 4 };
                    return Response.Json(new { counter.Count, pair.Value, pair.value });
                }
            }
            public sealed class Counter
            {
                private int count = 2;
                public int Count => count * 2;
            }
            public sealed class Pair
            {
                public int Value { get; init; }
                public int value { get; init; }
            }
            """);

        Assert.Contains("this.count$2 = 2;", module);
        Assert.Contains("get count()", module);
        Assert.Contains("Math.imul(this.count$2, 2)", module);
        Assert.Contains("this.value = 0;", module);
        Assert.Contains("this.value$2 = 0;", module);
        Assert.Contains("$workers$value.value = 3;", module);
        Assert.Contains("$workers$value.value$2 = 4;", module);
        Assert.Contains("return { value: this.value, value$2: this.value$2 };", module);
    }

    [Fact]
    public void KeepsFieldsFromShadowingTheJsonProjection()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new Model());
            }
            public sealed class Model
            {
                private object? toJSON;
                public int Value => toJSON is null ? 1 : 0;
            }
            """);

        Assert.Contains("this.toJSON$2 = null;", module);
        Assert.Contains("this.toJSON$2 == null", module);
        Assert.Contains("toJSON()", module);
        Assert.Contains("return { value: this.value };", module);
        Assert.DoesNotContain("this.toJSON =", module);
    }
}
