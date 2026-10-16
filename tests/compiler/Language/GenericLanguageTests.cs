namespace Workers.Compiler.Tests;

public sealed class GenericLanguageTests
{
    [Fact]
    public void SpecializesStaticAbstractInterfaceDispatch()
    {
        var module = Compile("""
            using Workers;
            public interface IValue { static abstract int Read(); }
            public sealed class Item : IValue { public static int Read() => 7; }
            public static class Worker
            {
                private static int Read<T>() where T : IValue => T.Read();
                [Fetch] public static Response Fetch(Request request) => new Response(Read<Item>().ToString());
            }
            """);
        Assert.Contains("return 7;", module);
    }
    [Fact]
    public void AppliesObjectInitializersToNewConstrainedTypes() => DifferentialRunner.Equal("""
        public class Base { public int Value { get; set; } }
        public sealed class Item : Base { }
        public static class Test
        {
            private static T Create<T>() where T : Base, new() => new T { Value = 7 };
            public static object Run() => new { value = Create<Item>().Value };
        }
        """);
    [Fact]
    public void KeepsGenericRecordRuntimeIdentityAndFormatsConcreteValues() => DifferentialRunner.Equal("""
        public sealed record Item<T>(T Value);
        public sealed record Other<T>(T Value);
        public static class Test
        {
            private static string Format<T>(T value) => $"{value}";
            public static object Run()
            {
                var item = new Item<string>("x");
                return new { a = item.Equals((object)new Other<string>("x")),
                    b = item.Equals((object)new Item<string>("x")), c = Format(7), d = Format(false) };
            }
        }
        """);
    [Fact]
    public void DiagnosesExpandingGenericRecursionInsteadOfEmittingForever()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) => new Response(Grow<int>().ToString());
                private static int Grow<T>() => Grow<List<T>>();
            }
            """));
        Assert.Contains("expanding generic recursion", error.Message);
    }
    [Fact]
    public void EvaluatesArgumentsBeforeRejectingNullGenericReceivers() => DifferentialRunner.Equal("""
        public sealed class Item { public int Read<T>(T value) => 7; }
        public static class Test
        {
            private static int calls;
            private static int Next() { calls++; return calls; }
            public static object Run()
            {
                Item item = null; var failed = false;
                try { item.Read(Next()); } catch (System.Exception) { failed = true; }
                return new { calls, failed };
            }
        }
        """);
    [Fact]
    public void GivesEachSpecializationItsOwnOutAndPatternLocalDeclarations() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string Read<T>(T value)
            {
                int.TryParse("7", out var number);
                object boxed = value;
                return boxed is string text ? text + number : "other" + number;
            }
            public static object Run() => new { a = Read("a"), b = Read(1), c = Read(false) };
        }
        """);
    [Fact]
    public void SupportsNewConstraintsAndGenericStaticInitialization() => DifferentialRunner.Equal("""
        public sealed class Box<T>
        {
            public static T Empty { get; set; }
            public static int Count;
            static Box() { Count = 7; }
            public Box() { Count++; }
        }
        public static class Test
        {
            private static T Create<T>() where T : new() => new T();
            public static object Run()
            {
                var a = Create<Box<int>>();
                var b = Create<Box<string>>();
                return new { a = Box<int>.Count, b = Box<string>.Count, c = Box<int>.Empty,
                    d = Box<string>.Empty, e = Create<int>(), f = Create<bool>() };
            }
        }
        """);
    [Fact]
    public void SupportsGenericRecordHierarchies() => DifferentialRunner.Equal("""
        public abstract record Result<T>(string Kind);
        public sealed record Success<T>(T Value) : Result<T>("success");
        public static class Test
        {
            public static object Run()
            {
                Result<string> value = new Success<string>("ok");
                return new { kind = value.Kind, value = ((Success<string>)value).Value };
            }
        }
        """);
    [Fact]
    public void DispatchesGenericVirtualAndInterfaceMethodsAndDelegates() => DifferentialRunner.Equal("""
        public interface IConvert { T Convert<T>(T value); }
        public class Base : IConvert
        {
            public virtual T Convert<T>(T value) => default(T);
        }
        public sealed class Child : Base
        {
            public override T Convert<T>(T value) => value;
        }
        public static class Test
        {
            public static object Run()
            {
                Base value = new Child(); IConvert contract = value;
                System.Func<string, string> convert = contract.Convert<string>;
                return new { a = value.Convert(7), b = contract.Convert("b"), c = convert("c") };
            }
        }
        """);

    [Fact]
    public void SupportsMultipleConstructedInterfacesAndVariance() => DifferentialRunner.Equal("""
        public interface IValue<out T> { T Value { get; } T Get(); }
        public sealed class Both : IValue<string>, IValue<object>
        {
            string IValue<string>.Value => "string";
            object IValue<object>.Value => "object";
            string IValue<string>.Get() => "get-string";
            object IValue<object>.Get() => "get-object";
        }
        public sealed class Text : IValue<string>
        {
            public string Value => "text";
            public string Get() => "get-text";
        }
        public static class Test
        {
            public static object Run()
            {
                var both = new Both(); IValue<string> a = both; IValue<object> b = both;
                IValue<object> c = new Text(); object source = new Text();
                return new { a = a.Value, b = b.Value, c = c.Value, d = a.Get(), e = b.Get(), f = c.Get(),
                    g = source is IValue<object>, h = ((IValue<object>)source).Value };
            }
        }
        """);
    [Fact]
    public void SupportsNestedGenericNamedCallsAndOutMethods() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static T Pick<T>(T first, T second) => first;
            private static T Outer<T>(T value) => Pick(second: default(T), first: value);
            private static bool TryValue<T>(T input, out T value) { value = input; return true; }
            public static object Run()
            {
                TryValue(7, out var value);
                return new { a = Outer(8), b = Outer("b"), value };
            }
        }
        """);
    [Fact]
    public void SpecializesDefaultsAndPreservesOriginalOverloadBinding() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string Pick(object value) => "object";
            private static string Pick(int value) => "int";
            private static string Bound<T>(T value) => Pick(value);
            private static T Empty<T>() => default(T);
            private static T Identity<T>(T value) => value;
            public static object Run() => new {
                a = Empty<int>(), b = Empty<bool>(), c = Empty<string>(),
                d = Identity(7), e = Identity("x"), f = Bound(7)
            };
        }
        """);

    [Fact]
    public void SupportsGenericClassesRecordsAndInstanceMethods() => DifferentialRunner.Equal("""
        public sealed class Box<T>
        {
            public T Value { get; set; }
            public Box(T value) { Value = value; }
            public T Get() => Value;
            public U Convert<U>(U fallback) => fallback;
        }
        public sealed record Item<T>(T Value);
        public static class Test
        {
            public static object Run()
            {
                var a = new Box<int>(7);
                var b = new Box<string>("b");
                var x = new Item<int>(4);
                var y = new Item<int>(4);
                return new { a = a.Get(), b = b.Get(), c = a.Convert("c"), equal = x == y,
                    text = x.ToString(), copied = (x with { Value = 8 }).Value };
            }
        }
        """);

    [Fact]
    public void KeepsStaticStateSeparateForEachConstructedType() => DifferentialRunner.Equal("""
        public sealed class Counter<T>
        {
            private static int count;
            public static int Next() { count++; return count; }
            public static T Empty() => default(T);
        }
        public static class Test
        {
            public static object Run() => new { a = Counter<int>.Next(), b = Counter<string>.Next(),
                c = Counter<int>.Next(), d = Counter<int>.Empty(), e = Counter<string>.Empty() };
        }
        """);

    [Fact]
    public void SupportsGenericExtensionsAndNestedCalls() => DifferentialRunner.Equal("""
        public static class Extensions
        {
            public static T Or<T>(this T value, T fallback) where T : class => value ?? fallback;
        }
        public static class Test
        {
            private static T Choose<T>(T value, T fallback) where T : class => value.Or(fallback);
            public static object Run() => new { a = Choose<string>(null, "a"), b = Choose("b", "c") };
        }
        """);
}
