namespace Workers.Compiler.Tests;

public sealed class LanguageExpansionRegressionTests
{
    [Fact]
    public void FormatsNestedRecordsWithCustomAndInheritedSealedText() => DifferentialRunner.Equal("""
        public sealed record Custom(int Value) { public override string ToString() => "custom"; }
        public record Base(int Value) { public sealed override string ToString() => "base"; }
        public sealed record Child(int Value) : Base(Value);
        public sealed record Envelope(Custom First, Child Second);
        public static class Test
        {
            public static object Run() => new { text = new Envelope(new Custom(1), new Child(2)).ToString() };
        }
        """);

    [Fact]
    public void DispatchesCustomRecordTextWithoutFormattingCollectionPayloads() => DifferentialRunner.Equal("""
        public abstract record Result<T>;
        public sealed record Success<T>(T Value) : Result<T>
        {
            public override string ToString() => "success";
        }
        public static class Test
        {
            public static object Run()
            {
                Result<System.Collections.Generic.List<string>> result = new Success<System.Collections.Generic.List<string>>(new());
                return new { text = result.ToString() };
            }
        }
        """);

    [Fact]
    public void DoesNotRequireUnusedRecordTextForCollectionPayloads() => DifferentialRunner.Equal("""
        public abstract record Result<T>;
        public sealed record Success<T>(T Value) : Result<T>;
        public static class Test
        {
            public static object Run()
            {
                var result = new Success<System.Collections.Generic.List<string>>(new System.Collections.Generic.List<string> { "a", "b" });
                return new { count = result.Value.Count };
            }
        }
        """);
    [Fact]
    public void DeclaresLambdaOutLocalsWhenNormalizingNamedArguments() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool Invoke(System.Func<bool> action, int count) => action();
            public static object Run() => new {
                expression = Invoke(count: 1, action: () => int.TryParse("7", out var value) && value == 7),
                block = Invoke(count: 1, action: () => { int.TryParse("8", out var value); return value == 8; })
            };
        }
        """);
    [Fact]
    public void InvokesSynthesizedBaseRecordTextWithVirtualPrintedMembers() => DifferentialRunner.Equal("""
        public record Base(int Value);
        public sealed record Child(int Value, int Extra) : Base(Value)
        {
            public override string ToString() => "child:" + base.ToString();
        }
        public static class Test
        {
            public static object Run() { Base value = new Child(3, 7); return new { text = value.ToString() }; }
        }
        """);

    [Fact]
    public void AppliesRecordObjectEqualsCastBeforeCustomTypedEquality() => DifferentialRunner.Equal("""
        public record Strange(int Value)
        {
            public virtual bool Equals(Strange other) => other is null;
            public override int GetHashCode() => 0;
        }
        public static class Test
        {
            public static object Run()
            {
                var value = new Strange(3);
                return new { a = value.Equals((object)null), b = value.Equals(new object()),
                    c = value == (Strange)null, d = (Strange)null == value };
            }
        }
        """);
    [Fact]
    public void RespectsExplicitPropertiesForPositionalRecordParameters() => DifferentialRunner.Equal("""
        public sealed record Computed(int Value) { public int Value => 7; }
        public sealed record Stored<T>(T Value) { public int Other => 3; public T Value { get; init; } = Value; }
        public static class Test
        {
            public static object Run()
            {
                var computed = new Computed(3);
                var stored = new Stored<string>("x");
                return new { a = computed.Value, b = computed.ToString(), c = stored.ToString() };
            }
        }
        """);

    [Fact]
    public void InvokesSynthesizedBaseRecordEqualityNonVirtually() => DifferentialRunner.Equal("""
        public record Base(int Value);
        public sealed record Child(int Value, int Extra) : Base(Value)
        {
            public bool Equals(Child other) => other is not null && base.Equals(other) && Extra % 2 == other.Extra % 2;
            public override int GetHashCode() => Value;
        }
        public static class Test
        {
            public static object Run() => new { a = new Child(3, 1) == new Child(3, 5), b = new Child(3, 1) == new Child(4, 1) };
        }
        """);
    [Fact]
    public void SupportsPrivateDefaultInterfaceHelpers() => DifferentialRunner.Equal("""
        public interface IValue
        {
            private int Initial => 3;
            private int Add(int value) => value + Initial;
            private T Identity<T>(T value) => value;
            int Read() => Identity(Add(4));
        }
        public sealed class Item : IValue { }
        public static class Test
        {
            public static object Run() { IValue value = new Item(); return new { value = value.Read() }; }
        }
        """);

    [Fact]
    public void PreservesCustomBaseEqualitySideEffectsForSelfComparisons() => DifferentialRunner.Equal("""
        public record Base(int Value)
        {
            public virtual bool Equals(Base other) { Test.Calls++; return false; }
            public override int GetHashCode() => 0;
        }
        public sealed record Child(int Value) : Base(Value);
        public static class Test
        {
            public static int Calls;
            public static object Run()
            {
                var value = new Child(1);
                var direct = value.Equals(value);
                var calls = Calls;
                Base same = value;
                return new { direct, calls, op = value == same, final = Calls };
            }
        }
        """);
    [Fact]
    public void ComparesNullRecordMembersByValue() => DifferentialRunner.Equal("""
        public abstract record Result;
        public sealed record Success(int Value) : Result;
        public sealed record Envelope(Result Value);
        public static class Test
        {
            public static object Run() => new {
                bothNull = new Envelope(null) == new Envelope(null),
                oneNull = new Envelope(null) == new Envelope(new Success(1)),
                equal = new Envelope(new Success(1)) == new Envelope(new Success(1))
            };
        }
        """);

    [Fact]
    public void DispatchesGenericDefaultInterfaceMethods() => DifferentialRunner.Equal("""
        public interface IValue { T Identity<T>(T value) => value; }
        public sealed class Item : IValue { }
        public static class Test
        {
            public static object Run()
            {
                IValue value = new Item();
                System.Func<string, string> identity = value.Identity<string>;
                return new { a = value.Identity(7), b = identity("b") };
            }
        }
        """);

    [Fact]
    public void EvaluatesExplicitBaseCopyArguments() => DifferentialRunner.Equal("""
        public record Base(int Value);
        public sealed record Child : Base
        {
            public Child() : base(3) { }
            private Child(Child original) : base(new Base(original.Value + 10)) { }
        }
        public static class Test
        {
            public static object Run()
            {
                Base source = new Child();
                return new { copied = (source with { }).Value, original = source.Value };
            }
        }
        """);

    [Fact]
    public void EvaluatesBaseArgumentsBeforeBaseStaticInitialization() => DifferentialRunner.Equal("""
        public class Base
        {
            static Base() { Test.Mark("static-base"); }
            public Base(int value) { Test.Mark("base-body"); }
        }
        public sealed class Child : Base
        {
            private int value = Test.Mark("field");
            static Child() { Test.Mark("static-child"); }
            public Child() : base(Test.Mark("argument")) { }
        }
        public static class Test
        {
            private static string trace = "";
            public static int Mark(string value) { trace += value + ";"; return 1; }
            public static object Run() { var child = new Child(); return new { trace }; }
        }
        """);

    [Fact]
    public void PreservesExceptionInitializerAndNamedBaseArgumentOrder() => DifferentialRunner.Equal("""
        public sealed class Failure : System.Exception
        {
            private int value = Test.Mark("field");
            public Failure() : base(innerException: Test.Inner(), message: Test.Message()) { }
        }
        public static class Test
        {
            private static string trace = "";
            public static int Mark(string value) { trace += value + ";"; return 1; }
            public static System.Exception Inner() { Mark("inner"); return null; }
            public static string Message() { Mark("message"); return "failed"; }
            public static object Run() { var failure = new Failure(); return new { trace, message = failure.Message }; }
        }
        """);

    [Fact]
    public void UsesMostSpecificDefaultInterfaceImplementation() => DifferentialRunner.Equal("""
        public interface IBase { int Read() => 1; }
        public interface IChild : IBase { int IBase.Read() => 2; }
        public sealed class Item : IChild { }
        public static class Test
        {
            public static object Run() { IBase value = new Item(); return new { value = value.Read() }; }
        }
        """);
}
