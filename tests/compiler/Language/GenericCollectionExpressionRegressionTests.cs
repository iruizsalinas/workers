namespace Workers.Compiler.Tests;

public sealed class GenericCollectionExpressionRegressionTests
{
    [Theory]
    [InlineData("[first, second]", "AB")]
    [InlineData("[.. new[] { first, second }]", "AB")]
    [InlineData("[]", "")]
    public void SpecializedByteCollectionExpressionsProduceNativeByteBuffers(string expression, string expected)
    {
        var actual = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static T[] Values<T>(T first, T second) => {{expression}};
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var bytes = Values<byte>(65, 66);
                    return Response.Json(new TextDecoder().Decode(bytes));
                }
            }
            """);
        Assert.Equal(expected, actual.GetString());
    }

    [Fact]
    public void SpecializedSetExpressionsUseTheConcreteElementType() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static HashSet<T> Values<T>(T value) => [value, value];
            public static object Run() => Values(7).Count;
        }
        """);

    [Fact]
    public void SpecializedObjectSpreadsApplyBoxingConversions() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            private static T[] Copy<T, U>(U[] values) where U : T => [.. values];
            public static object Run()
            {
                var values = Copy<object, Status>(new[] { Status.Ready });
                return new { matched = values[0] is Status.Ready, text = values[0].ToString() };
            }
        }
        """);
}
