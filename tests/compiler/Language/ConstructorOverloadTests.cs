namespace Workers.Compiler.Tests;

public sealed class ConstructorOverloadTests
{
    [Fact]
    public void PreservesDefaultMessagesInUserExceptionHierarchies() => DifferentialRunner.Equal("""
        public class Failure : System.Exception { }
        public sealed class SpecificFailure : Failure { }
        public static class Test
        {
            public static object Run()
            {
                try { throw new SpecificFailure(); }
                catch (Failure error) { return new { message = error.Message }; }
            }
        }
        """);
    [Fact]
    public void PreservesOverloadBindingChainingAndInitializationOrder() => DifferentialRunner.Equal("""
        public sealed class Model
        {
            public static string Log = "";
            private int value = Mark(8);
            public int Value => value;
            public static int Mark(int value) { Log += value; return value; }
            public Model() : this(Mark(1), second: Mark(2)) { Log += "a"; }
            public Model(int first, int second = 3) { value += first + second; Log += "b"; }
            public Model(string text) : this() { Log += text; }
        }
        public static class Test
        {
            public static object Run()
            {
                var a = new Model();
                var b = new Model(second: Model.Mark(4), first: Model.Mark(5));
                var c = new Model(6);
                var d = new Model("d");
                return new { a = a.Value, b = b.Value, c = c.Value, d = d.Value, log = Model.Log };
            }
        }
        """);

    [Fact]
    public void SupportsPositionalRecordsAndEarlyConstructorReturns() => DifferentialRunner.Equal("""
        public sealed record Item(int Value)
        {
            public Item() : this(7) { }
            public Item(string value) : this() { if (value == null) return; Value += 2; }
        }
        public static class Test
        {
            public static object Run()
            {
                var a = new Item();
                var b = new Item("b");
                var c = new Item((string)null);
                var d = b with { Value = 12 };
                return new { a = a.Value, b = b.Value, c = c.Value, d = d.Value };
            }
        }
        """);

    [Fact]
    public void SupportsChainedExceptionConstructors() => DifferentialRunner.Equal("""
        public sealed class Failure : System.Exception
        {
            public Failure() : this("failed") { }
            public Failure(string message) : base(message) { }
        }
        public static class Test
        {
            public static object Run()
            {
                try { throw new Failure(); }
                catch (Failure error) { return new { message = error.Message }; }
            }
        }
        """);
}
