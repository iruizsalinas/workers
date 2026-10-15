namespace Workers.Compiler.Tests;

public sealed class IteratorRegressionTests
{
    [Fact]
    public void IteratorSequencesCanBeEnumeratedAgainAfterEarlyDisposal() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int runs;
            private static int disposals;
            public static object Run()
            {
                var sequence = Values(2);
                var before = runs;
                var first = sequence.First();
                var all = sequence.ToArray();
                var again = sequence.ToArray();
                var pairs = sequence.SelectMany(left => sequence, (left, right) => left * 10 + right).ToArray();
                return new { before, first, all, again, pairs, runs, disposals };
            }
            private static IEnumerable<int> Values(int start)
            {
                runs++;
                try { yield return start++; yield return start++; }
                finally { disposals++; }
            }
        }
        """);

    [Fact]
    public void InstanceIteratorsKeepTheirReceiverAndResetParameters() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var counter = new Counter { Offset = 10 };
                var sequence = counter.Values(2);
                var first = sequence.ToArray();
                counter.Offset = 20;
                var second = sequence.ToArray();
                return new { first, second };
            }
        }
        public sealed class Counter
        {
            public int Offset { get; set; }
            public IEnumerable<int> Values(int start)
            {
                yield return Offset + start++;
                yield return Offset + start++;
            }
        }
        """);

    [Fact]
    public void AsyncIteratorSequencesCanBeEnumeratedAgain() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var values = Values(2);
                var first = new List<int>();
                var second = new List<int>();
                await foreach (var value in values) first.Add(value);
                await foreach (var value in values) second.Add(value);
                return new { first, second };
            }
            private static async IAsyncEnumerable<int> Values(int start)
            {
                await Task.FromResult(0);
                yield return start++;
                yield return start++;
            }
        }
        """);
}
