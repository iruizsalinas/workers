internal static partial class HelperSource
{
    private static string QueueMessages(Func<string, string> name) => $$"""
        const {{name("queueBodies")}} = new WeakMap();
        function {{name("queueBody")}}(message, read) {
          if (!{{name("queueBodies")}}.has(message)) {{name("queueBodies")}}.set(message, read(message.body));
          return {{name("queueBodies")}}.get(message);
        }
        function {{name("queueSend")}}(queue, body, options) {
          return queue.send(options?.contentType === "v8" ? {{name("rpcValue")}}(body) : body, options);
        }
        function {{name("queueSendBatch")}}(queue, messages, options) {
          return queue.sendBatch(Array.from(messages, message => message.contentType === "v8"
            ? { ...message, body: {{name("rpcValue")}}(message.body) } : message), options);
        }

        """;
}
