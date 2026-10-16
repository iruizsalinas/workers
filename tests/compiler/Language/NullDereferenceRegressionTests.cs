namespace Workers.Compiler.Tests;

public sealed class NullDereferenceRegressionTests
{
    [Fact]
    public void ReadsThrowTypedNullReferenceExceptions() => DifferentialRunner.Equal("""
        public sealed class Row { public int Field; public int Value => 1; }
        public static class Test
        {
            static bool Check(Action action) { try { action(); return false; } catch (Exception e) { return e is NullReferenceException && e is SystemException; } }
            public static object Run()
            {
                string text = null!; Row row = null!; int[] array = null!; List<int> list = null!;
                return new { text = Check(() => { var n = text.Length; }), field = Check(() => { var n = row.Field; }), property = Check(() => { var n = row.Value; }), array = Check(() => { var n = array.Length; }), list = Check(() => { var n = list.Count; }) };
            }
        }
        """);

    [Fact]
    public void InstanceCallsEvaluateArgumentsBeforeThrowing() => DifferentialRunner.Equal("""
        public sealed class Row { public int Method(int value) => value; public T Generic<T>(T value) => value; }
        public static class Test
        {
            static int count;
            static int Next() => ++count;
            static bool Check(Action action) { try { action(); return false; } catch (NullReferenceException) { return true; } }
            public static object Run()
            {
                Row row = null!; string text = null!; List<int> list = null!; Func<int, int> callback = null!;
                var method = Check(() => row.Method(Next())); var generic = Check(() => row.Generic(Next()));
                var native = Check(() => text.Contains(Next().ToString())); var collection = Check(() => list.Add(Next()));
                var invoke = Check(() => callback(Next()));
                return new { method, generic, native, collection, invoke, count };
            }
        }
        """);

    [Fact]
    public void AssignmentsAndMutationsPreserveEvaluationOrder() => DifferentialRunner.Equal("""
        public sealed class Row { public int Field; public int Value { get; set; } public string Text { get; set; } = ""; public int this[int i] { get => 0; set {} } }
        public static class Test
        {
            static int count;
            static Row Receiver() { count = count * 10 + 1; return null!; }
            static int Next() { count = count * 10 + 2; return 3; }
            static int Check(Action action) { count = 0; try { action(); } catch (NullReferenceException) { return count; } catch (Exception) { return -count; } return -1; }
            public static object Run() => new {
                field = Check(() => Receiver().Field = Next()), property = Check(() => Receiver().Value = Next()),
                compound = Check(() => Receiver().Value += Next()), increment = Check(() => Receiver().Field++),
                coalesce = Check(() => Receiver().Text ??= Next().ToString()), indexer = Check(() => Receiver()[Next()] = Next())
            };
        }
        """);

    [Fact]
    public void NullConditionalAndExtensionReceiversRemainValid() => DifferentialRunner.Equal("""
        public static class Extensions { public static bool Missing(this string value) => value == null; }
        public static class Test
        {
            static int count;
            static string Next() { count++; return "x"; }
            public static object Run() { string text = null!; return new { length = text?.Length, contains = text?.Contains(Next()), extension = text.Missing(), count }; }
        }
        """);

    [Fact]
    public void AsyncArgumentsAndAssignmentsKeepTheirOrdering() => DifferentialRunner.Equal("""
        public sealed class Row { public int Value { get; set; } public int Method(int value) => value; }
        public static class Test
        {
            static int count;
            static async Task<int> Next() { await Task.CompletedTask; return ++count; }
            public static async Task<object> Run()
            {
                Row row = null!; var call = false; var assignment = false;
                try { row.Method(await Next()); } catch (NullReferenceException) { call = true; }
                try { row.Value = await Next(); } catch (NullReferenceException) { assignment = true; }
                return new { call, assignment, count };
            }
        }
        """);

    [Fact]
    public void IndexingEnumerationAndTryMethodsThrowTypedExceptions() => DifferentialRunner.Equal("""
        public static class Test
        {
            static bool Check(Action action) { try { action(); return false; } catch (NullReferenceException) { return true; } }
            public static object Run()
            {
                Dictionary<int, int> numbers = null!; Dictionary<string, int> names = null!; Queue<int> queue = null!; int[] array = null!;
                return new {
                    numbers = Check(() => { var n = numbers[1]; }), names = Check(() => names.TryGetValue("x", out var value)),
                    nameIndex = Check(() => { var n = names[null!]; }), nameWrite = Check(() => names[null!] = 2),
                    queue = Check(() => queue.TryDequeue(out var value)),
                    enumeration = Check(() => { foreach (var n in array) {} }), write = Check(() => numbers[1] = 2)
                };
            }
        }
        """);

    [Fact]
    public void RecordsEventsAndMethodGroupsThrowTypedExceptions() => DifferentialRunner.Equal("""
        public sealed record Row(int Value);
        public sealed class Publisher { public event Action? Changed; public void Method() {} }
        public static class Test
        {
            static bool Check(Action action) { try { action(); return false; } catch (NullReferenceException) { return true; } }
            public static object Run()
            {
                Row row = null!; Publisher publisher = null!;
                return new { copy = Check(() => { var n = row with { Value = 2 }; }), text = Check(() => row.ToString()),
                    subscription = Check(() => publisher.Changed += () => {}) };
            }
        }
        """);

    [Fact]
    public void ArgumentExceptionsTakePrecedenceOverNullReceivers() => DifferentialRunner.Equal("""
        public sealed class Row { public int Field; public void Method(int value) {} }
        public static class Test
        {
            static int Fail() => throw new FormatException("argument");
            static bool Check(Action action) { try { action(); return false; } catch (FormatException) { return true; } catch (Exception) { return false; } }
            public static object Run()
            {
                Row row = null!; List<int> list = null!; Func<int, int> callback = null!;
                return new { call = Check(() => row.Method(Fail())), assign = Check(() => row.Field = Fail()), list = Check(() => list.Add(Fail())), callback = Check(() => callback(Fail())) };
            }
        }
        """);

    [Fact]
    public void DelegateBindingAndArraySlicingUseTheirSpecificExceptionTypes() => DifferentialRunner.Equal("""
        public class Row { public void Method() {} public virtual void Virtual() {} }
        public static class Test
        {
            static string Check(Action action) { try { action(); return "none"; } catch (NullReferenceException) { return "null"; } catch (ArgumentNullException) { return "argument-null"; } catch (ArgumentException) { return "argument"; } }
            public static object Run()
            {
                Row row = null!; int[] array = null!; string text = null!;
                return new { method = Check(() => { Action callback = row.Method; }), virtualMethod = Check(() => { Action callback = row.Virtual; }), array = Check(() => { var slice = array[..]; }), text = Check(() => { var slice = text[..]; }) };
            }
        }
        """);

    [Fact]
    public void BindingCallsEvaluateAwaitedArgumentsOnceBeforeCheckingTheReceiver()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                static int count;
                static async Task<string> Value() { await Task.CompletedTask; count++; return "value"; }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    Headers headers = null!; var caught = false;
                    try { headers.Set("x-test", await Value()); } catch (NullReferenceException) { caught = true; }
                    return Response.Json(new { count, caught });
                }
            }
            """);
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("caught").GetBoolean());
    }

    [Fact]
    public void PlatformTypeErrorsAreNotNullReferenceExceptions()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    try { var input = new Request("not a URL"); return Response.Json(false); }
                    catch (Exception e) { return Response.Json(e is NullReferenceException); }
                }
            }
            """);
        Assert.False(result.GetBoolean());
    }
}
