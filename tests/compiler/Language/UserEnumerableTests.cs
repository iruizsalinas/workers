namespace Workers.Compiler.Tests;

public sealed class UserEnumerableTests
{
    private const string RangeDeclaration = """
        public class Range3 : System.Collections.Generic.IEnumerable<int>
        {
            public System.Collections.Generic.IEnumerator<int> GetEnumerator()
            {
                yield return 1; yield return 2; yield return 3;
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public sealed class DerivedRange : Range3 { }
        """;

    [Fact]
    public void RejectsForeachOverSourceEnumerable() => RejectsSourceForeach("""
        var total = 0;
        foreach (var value in new Range3()) total += value;
        return Response.Json(total);
        """, RangeDeclaration);

    [Theory]
    [InlineData("return Response.Json(new Range3().ToArray());")]
    [InlineData("IEnumerable<int> values = new Range3(); return Response.Json(values.ToArray());")]
    [InlineData("return Response.Json(new DerivedRange());")]
    public void RejectsSourceEnumerableBeforeEmittingClass(string body)
    {
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker(body, RangeDeclaration));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("unsupported enumeration protocol", error.Message);
    }

    [Fact]
    public void RejectsInterfaceInheritedEnumerableContract()
    {
        var declarations = RangeDeclaration.Replace(
            "public class Range3 : System.Collections.Generic.IEnumerable<int>",
            "public interface IRange : System.Collections.Generic.IEnumerable<int> { } public class Range3 : IRange");
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker("return Response.Json(new Range3());", declarations));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("unsupported enumeration protocol", error.Message);
    }

    [Fact]
    public void RejectsNonGenericEnumerableContract()
    {
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker("return Response.Json(new Range3());", """
            public sealed class Range3 : System.Collections.IEnumerable
            {
                public System.Collections.IEnumerator GetEnumerator() { yield return 1; }
            }
            """));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("unsupported enumeration protocol", error.Message);
    }

    [Theory]
    [InlineData("WorkerEntrypoint")]
    [InlineData("DurableObject")]
    [InlineData("HtmlElementHandler")]
    public void RejectsEnumerableContractOnGeneratedClasses(string baseClass)
    {
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker("return new Response(\"ok\");", $$"""
            public sealed class Range3 : {{baseClass}}, System.Collections.IEnumerable
            {
                public System.Collections.IEnumerator GetEnumerator() { yield return 1; }
            }
            """));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("unsupported enumeration protocol", error.Message);
    }

    [Fact]
    public void RejectsAsyncEnumerableContract()
    {
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker("return Response.Json(new Range3());", """
            public sealed class Range3 : IAsyncEnumerable<int>
            {
                public async IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default)
                {
                    await Task.CompletedTask;
                    yield return 1;
                }
            }
            """));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("unsupported enumeration protocol", error.Message);
    }

    [Fact]
    public void RejectsSourceEnumerationPatternWithoutInterface() => RejectsSourceForeach("""
        var total = 0;
        foreach (var value in new Range3()) total += value;
        return Response.Json(total);
        """, """
        public sealed class Range3
        {
            public System.Collections.Generic.IEnumerator<int> GetEnumerator() { yield return 1; }
        }
        """);

    [Fact]
    public void RejectsSourceAsyncEnumerationPatternWithoutInterface() => RejectsSourceForeach("""
        await foreach (var value in new Range3()) { }
        return Response.Json(0);
        """, """
        public sealed class Range3
        {
            public async IAsyncEnumerator<int> GetAsyncEnumerator()
            {
                await Task.CompletedTask;
                yield return 1;
            }
        }
        """, isAsync: true);

    private static void RejectsSourceForeach(string body, string declarations, bool isAsync = false)
    {
        var error = Assert.Throws<NotSupportedException>(() => CompileWorker(body, declarations, isAsync));
        Assert.StartsWith("WRK108:", error.Message);
        Assert.Contains("Source-defined foreach enumeration protocols", error.Message);
    }

    private static string CompileWorker(string body, string declarations, bool isAsync = false) => Compile($$"""
        using Workers;
        public static class Worker
        {
            [Fetch] public static {{(isAsync ? "async Task<Response>" : "Response")}} Fetch(Request request)
            {
                {{body}}
            }
        }
        {{declarations}}
        """);
}
