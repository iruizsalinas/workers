// The generated example modules are imported directly by the tests. This main
// Worker supplies a workerd isolate and locally simulated bindings to them.
export default {
  fetch() {
    return new Response("Workers C# runtime test harness");
  },
};
