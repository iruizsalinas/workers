namespace Workers.Compiler.Tests;

public sealed class GeneratedClassValidationTests
{
    [Theory]
    [InlineData("public abstract class Counter { }")]
    [InlineData("public class Counter<T> { }")]
    [InlineData("public class Counter : CustomBase { }")]
    [InlineData("public class Counter { ~Counter() { } }")]
    [InlineData("public class Counter { public int this[int index] => index; }")]
    [InlineData("public class Counter { public event Action? Changed; }")]
    [InlineData("public class Counter { public const int Value = 1; }")]
    [InlineData("public class Counter { static Counter() { } }")]
    public void RejectsUnsupportedDurableObjectShapes(string declaration)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public class CustomBase { }
            [DurableObject("Counter")]
            {{declaration}}
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsAttributedRecordClasses()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            [DurableObject("Counter")]
            public record Counter(int Value);
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsNestedGeneratedClasses()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Container
            {
                [DurableObject("Counter")]
                public class Counter { }
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsUnexpectedWorkerEntrypointBaseClasses()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public class CustomEntrypoint : WorkerEntrypoint { }
            [WorkerEntrypoint("Entry")]
            public class Entry : CustomEntrypoint { }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsPartialHtmlHandlersAsOneGeneratedType()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public partial class Handler : HtmlElementHandler
            {
                public override ValueTask ElementAsync(HtmlElement element) => ValueTask.CompletedTask;
            }
            public partial class Handler { }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(Response.Html("<p>x</p>"));
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsHtmlHandlersWithIntermediateBaseClasses()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public class CustomHandler : HtmlElementHandler
            {
                public override ValueTask ElementAsync(HtmlElement element) => ValueTask.CompletedTask;
            }
            public class Handler : CustomHandler { }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(Response.Html("<p>x</p>"));
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsAbstractGeneratedMethods()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            [DurableObject("Counter")]
            public abstract class Counter
            {
                public abstract int Value();
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }
}
