namespace Workers.Compiler.Tests;

public sealed class GeneratedClassValidationTests
{
    [Theory]
    [InlineData("public abstract class Counter : DurableObject { }")]
    [InlineData("public class Counter<T> : DurableObject { }")]
    [InlineData("public class Counter : CustomBase { }")]
    [InlineData("public class Counter : DurableObject { ~Counter() { } }")]
    [InlineData("public class Counter : DurableObject { public int this[int index] => index; }")]
    [InlineData("public class Counter : DurableObject { public event Action? Changed; }")]
    [InlineData("public class Counter : DurableObject { static Counter() { } }")]
    public void RejectsUnsupportedDurableObjectShapes(string declaration)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public class CustomBase : DurableObject { }
            {{declaration}}
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
                public class Counter : DurableObject { }
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
                public override Task ElementAsync(HtmlElement element) => Task.CompletedTask;
            }
            public partial class Handler { }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(new Response("<p>x</p>") { Headers = new Headers { ["content-type"] = "text/html; charset=utf-8" } });
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
                public override Task ElementAsync(HtmlElement element) => Task.CompletedTask;
            }
            public class Handler : CustomHandler { }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(new Response("<p>x</p>") { Headers = new Headers { ["content-type"] = "text/html; charset=utf-8" } });
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Fact]
    public void RejectsAbstractGeneratedMethods()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public abstract class Counter : DurableObject
            {
                public abstract int Value();
            }
            """));

        Assert.StartsWith("WRK116:", error.Message);
    }

    [Theory]
    [InlineData("constructor")]
    [InlineData("Constructor")]
    [InlineData("ConstructorAsync")]
    public void RejectsPublicMethodsThatNormalizeToConstructor(string methodName)
    {
        var returnType = methodName.EndsWith("Async", StringComparison.Ordinal) ? "Task" : "void";
        var body = returnType == "Task" ? " => Task.CompletedTask;" : " { }";
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public class Example : DurableObject
            {
                public {{returnType}} {{methodName}}(){{body}}
            }
            """));

        Assert.StartsWith("WRK118:", error.Message);
        Assert.Contains("reserved JavaScript class method name 'constructor'", error.Message);
    }

    [Fact]
    public void RejectsPrivateConstructorNamedHelpersThatWouldProduceInvalidJavascript()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public class Example : DurableObject
            {
                private void constructor() { }
                public void Run() => constructor();
            }
            """));

        Assert.StartsWith("WRK118:", error.Message);
        Assert.Contains("reserved JavaScript class method name 'constructor'", error.Message);
    }

    [Fact]
    public void RejectsConstructorNamedWorkerEntrypointMethods()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public class Example : WorkerEntrypoint
            {
                public void constructor() { }
            }
            """));

        Assert.StartsWith("WRK118:", error.Message);
    }

    [Fact]
    public void RejectsConstructorNamedHtmlHandlerMethods()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public class Handler : HtmlElementHandler
            {
                public void constructor() { }
                public override Task ElementAsync(HtmlElement element) => Task.CompletedTask;
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(new Response("<p>x</p>") { Headers = new Headers { ["content-type"] = "text/html; charset=utf-8" } });
            }
            """));

        Assert.StartsWith("WRK118:", error.Message);
    }
}
