namespace Workers.Compiler.Tests;

public sealed class QueueStackTests
{
    [Fact]
    public void EmitsQueueAndStackOperationsWithClrEnumerationOrder()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var queue = new Queue<int>(new List<int> { 1, 2 });
                    queue.Enqueue(3);
                    var stack = new Stack<string>(new List<string> { "bottom", "top" });
                    stack.Push("new");
                    return Response.Json(new {
                        queueCount = queue.Count, queueHead = queue.Peek(), removed = queue.Dequeue(),
                        queueContains = queue.Contains(2), queueValues = queue.ToArray(),
                        stackCount = stack.Count, stackHead = stack.Peek(), popped = stack.Pop(),
                        stackContains = stack.Contains("top"), stackValues = stack.ToArray()
                    });
                }
            }
            """);

        Assert.Contains("$workers$queueStackCreate([1, 2], false, null)", module);
        Assert.Contains("queue.push(3)", module);
        Assert.Contains("$workers$queueStackTake(queue, false)", module);
        Assert.Contains("$workers$queueStackTake(queue, true)", module);
        Assert.Contains("$workers$queueStackCreate([\"bottom\", \"top\"], true, null)", module);
        Assert.Contains("stack.unshift(\"new\")", module);
        Assert.Contains("stack.includes(\"top\")", module);
        Assert.Contains("stack.slice()", module);
    }

    [Fact]
    public void EmitsCapacityConstructionClearAndForeach()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var queue = new Queue<int>(capacity: 4);
                    var stack = new Stack<int>();
                    queue.Enqueue(1);
                    stack.Push(2);
                    var total = 0;
                    foreach (var value in queue) total += value;
                    foreach (var value in stack) total += value;
                    queue.Clear();
                    stack.Clear();
                    return Response.Json(new { total, queueCount = queue.Count, stackCount = stack.Count });
                }
            }
            """);

        Assert.Contains("$workers$queueStackCreate(null, false, 4)", module);
        Assert.Contains("for (const value of queue)", module);
        Assert.Contains(" of stack) {", module);
        Assert.Contains("queue.length = 0", module);
        Assert.Contains("stack.length = 0", module);
    }

    [Theory]
    [InlineData("queue.TryDequeue(out value)")]
    [InlineData("queue.TryPeek(out value)")]
    [InlineData("queue.TrimExcess()")]
    public void RejectsQueueApisOutsideTheFocusedProfile(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var queue = new Queue<int>();
                    var value = 0;
                    {{operation}};
                    return Response.Empty();
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}

