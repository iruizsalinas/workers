internal static partial class HelperSource
{
    private static string CancellationCheck(Func<string, string> name) => $$"""
        function {{name("cancellationError")}}() {
          return new DOMException("The operation was canceled.", "AbortError");
        }
        function {{name("cancellationCheck")}}(signal) {
          if (signal?.aborted) throw {{name("cancellationError")}}();
        }

        """;

    private static string CancellationDelay(Func<string, string> name) => $$"""
        function {{name("cancellationDelay")}}(milliseconds, signal) {
          if (signal == null) return {{name("delay")}}(milliseconds);
          if (milliseconds < -1 || milliseconds > 4294967294)
            throw new RangeError("Delay is out of range.");
          if (signal.aborted) return Promise.reject({{name("cancellationError")}}());
          return new Promise((resolve, reject) => {
            let timer;
            const cleanup = () => signal.removeEventListener("abort", aborted);
            const completed = () => { cleanup(); resolve(); };
            const aborted = () => {
              if (timer !== undefined) clearTimeout(timer);
              cleanup();
              reject({{name("cancellationError")}}());
            };
            signal.addEventListener("abort", aborted, { once: true });
            if (milliseconds !== -1) timer = setTimeout(completed, milliseconds);
          });
        }

        """;

    private static string CancellationCancelAfter(Func<string, string> name) => $$"""
        const {{name("cancellationTimers")}} = new WeakMap();
        function {{name("cancellationCancelAfter")}}(controller, milliseconds) {
          if (!Number.isInteger(milliseconds) || milliseconds < -1 || milliseconds > 2147483647)
            throw new RangeError("Cancellation delay is out of range.");
          const existing = {{name("cancellationTimers")}}.get(controller);
          if (existing !== undefined) {
            clearTimeout(existing.timer);
            controller.signal.removeEventListener("abort", existing.aborted);
          }
          if (milliseconds === -1 || controller.signal.aborted) {
            {{name("cancellationTimers")}}.delete(controller);
            return;
          }
          let timer;
          const aborted = () => {
            clearTimeout(timer);
            {{name("cancellationTimers")}}.delete(controller);
          };
          timer = setTimeout(() => {
            controller.abort();
          }, milliseconds);
          {{name("cancellationTimers")}}.set(controller, { timer, aborted });
          controller.signal.addEventListener("abort", aborted, { once: true });
        }

        """;
}
