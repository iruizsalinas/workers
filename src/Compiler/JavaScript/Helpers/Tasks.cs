internal static partial class HelperSource
{
    private static string AsyncExpressionResult(Func<string, string> name) => $$"""
        function {{name("asyncExpressionResult")}}(state) {
          if (state.failed) throw state.error;
          return state.value;
        }

        """;

    // Task.WhenAll waits for every task, then throws the first failure in task order.
    private static string TaskWhenAll(Func<string, string> name) => $$"""
        function {{name("taskWhenAll")}}(tasks) {
          if (tasks == null) throw new TypeError("Tasks cannot be null.");
          const values = Array.from(tasks);
          if (values.some(task => task == null)) throw new TypeError("A task cannot be null.");
          return Promise.allSettled(values).then(results => {
            const failure = results.find(result => result.status === "rejected");
            if (failure) throw failure.reason;
            return results.map(result => result.value);
          });
        }

        """;
}
