internal static partial class HelperSource
{
    // Task.WhenAll waits for every task, then throws the first failure in task order.
    private static string TaskWhenAll(Func<string, string> name) => $$"""
        async function {{name("taskWhenAll")}}(tasks) {
          const results = await Promise.allSettled(tasks);
          const failure = results.find(result => result.status === "rejected");
          if (failure) throw failure.reason;
          return results.map(result => result.value);
        }

        """;
}
