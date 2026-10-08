internal static partial class HelperSource
{
    // WhenAll waits for every task, including after failure. Generic overloads inspect failures
    // in submission order; non-generic overloads keep completion order. Faults outrank cancellation.
    private static string TaskWhenAll(Func<string, string> name) => $$"""
        function {{name("taskWhenAll")}}(source, generic) {
          if (source == null) throw new TypeError("Task collection cannot be null.");
          let tasks;
          if (generic) {
            tasks = [];
            const iterator = source[Symbol.iterator]();
            try {
              while (true) {
                const item = iterator.next();
                if (item.done) break;
                if (item.value == null) throw new TypeError("Tasks cannot be null.");
                tasks.push(item.value);
              }
            } finally { iterator.return?.(); }
          } else {
            tasks = Array.from(source);
            if (tasks.some(task => task == null)) throw new TypeError("Tasks cannot be null.");
          }
          const failures = [];
          const completed = tasks.map((task, index) => Promise.resolve(task).then(
            value => value,
            error => { failures.push({ error, index, cancelled: error?.name === "AbortError" }); }
          ));
          return Promise.all(completed).then(values => {
            if (generic) failures.sort((left, right) => left.index - right.index);
            const failure = failures.find(item => !item.cancelled) ?? failures[0];
            if (failure) throw failure.error;
            return generic ? values : undefined;
          });
        }

        """;
}
