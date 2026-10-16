namespace Workers.Compiler.Tests;

public sealed class CollectionValueSemanticsTests
{
    [Fact]
    public void RangesQueueConstructorsAndOutValuesCopyTupleStorage() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var source = new[] { (1, 2), (3, 4) };
                var slice = source[..1];
                var queue = new Queue<(int, int)>(source); var stack = new Stack<(int, int)>(source);
                var dictionary = new Dictionary<string, (int, int)> { ["x"] = source[0] };
                source[0].Item1 = 9;
                queue.TryPeek(out var peeked); peeked.Item1 = 7;
                dictionary.TryGetValue("x", out var value); value.Item1 = 8;
                return new { sliced = slice[0].Item1, queued = queue.Peek().Item1, stacked = stack.ToArray()[1].Item1,
                    value = dictionary["x"].Item1, peeked = peeked.Item1 };
            }
        }
        """);
    [Fact]
    public void NativeCallbacksAndMulticastHandlersReceiveTupleValueCopies() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int Change((int, int) item) { item.Item1 = 8; return item.Item1; }
            public static object Run()
            {
                var array = new[] { (1, 2) };
                var first = array.Select(item => { item.Item1 = 7; return item.Item1; }).ToArray();
                var second = array.Select(Change).ToArray();
                Func<(int, int), int> change = Change;
                Func<(int, int), int> read = item => item.Item1;
                var combined = change + read;
                var result = combined(array[0]);
                return new { first, second, result, stored = array[0].Item1 };
            }
        }
        """);
    [Fact]
    public void ListAndCollectionInitializersCopyTupleValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var tuple = (1, 2);
                var list = new List<(int, int)> { tuple };
                List<(int, int)> expression = [tuple];
                (int, int)[] array = [tuple];
                tuple.Item1 = 9;
                return new { list = list[0].Item1, expression = expression[0].Item1, array = array[0].Item1 };
            }
        }
        """);

    [Fact]
    public void ForeachTupleLocalsAreCopiesOfArrayAndListElements() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var array = new[] { (1, 2) }; var list = new List<(int, int)> { (3, 4) };
                var callbacks = new List<Func<int>>();
                foreach (var item in array) callbacks.Add(() => item.Item1);
                foreach (var item in list) callbacks.Add(() => item.Item2);
                array[0].Item1 = 7; list[0] = (8, 9);
                return new { captures = callbacks.Select(callback => callback()).ToArray(), first = array[0].Item1, second = list[0].Item2 };
            }
        }
        """);

    [Fact]
    public void ListMaterializationAndAddRangeCopyElements() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var array = new[] { (1, 2) };
                var constructed = new List<(int, int)>(array);
                var added = new List<(int, int)>(); added.AddRange(array);
                var materialized = array.ToList(); var copied = array.ToArray();
                array[0].Item1 = 9;
                var listArray = constructed.ToArray(); listArray[0].Item1 = 7;
                return new { constructed = constructed[0].Item1, added = added[0].Item1, materialized = materialized[0].Item1,
                    copied = copied[0].Item1, listArray = listArray[0].Item1 };
            }
        }
        """);

    [Fact]
    public void SortWrapsComparerFailuresAndKeepsTheOriginalException() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var caught = false; var inner = false;
                try { new List<int> { 2, 1 }.Sort((a, b) => throw new FormatException("comparison")); }
                catch (InvalidOperationException exception) { caught = true; inner = exception.InnerException is FormatException; }
                return new { caught, inner };
            }
        }
        """);
}
