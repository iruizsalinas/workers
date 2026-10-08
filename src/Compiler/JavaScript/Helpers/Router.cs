internal static partial class HelperSource
{
    // A router is a list of routes kept in specificity order: per segment, literals come before constrained
    // parameters, then parameters, then catch-alls, and a route that ends earlier comes first. Equally specific
    // routes keep method routes before Any routes, then registration order. Dispatch matches the decoded path
    // segments, lets GET routes answer HEAD without a body, and answers 405 with Allow when only the method
    // differs, before trying the fallback and finally 404.
    private static string Router(Func<string, string> name) => $$"""
        function {{name("routerCreate")}}() {
          return { routes: [], fallback: null };
        }
        function {{name("routerAdd")}}(router, method, route, handler) {
          const existing = router.routes.find(entry => entry.method === method && entry.route.key === route.key);
          if (existing !== undefined)
            throw new Error(`The route ${method ?? "ANY"} ${route.pattern} conflicts with ${existing.route.pattern}, which matches the same paths.`);
          router.routes.push({ method, route, handler, order: router.routes.length });
          router.routes.sort({{name("routerCompare")}});
          return router;
        }
        function {{name("routerCompare")}}(left, right) {
          const a = left.route.rank, b = right.route.rank;
          for (let index = 0; index < Math.max(a.length, b.length); index++) {
            const difference = (a[index] ?? -1) - (b[index] ?? -1);
            if (difference !== 0) return difference;
          }
          return (left.method === null) - (right.method === null) || left.order - right.order;
        }
        function {{name("routerFallback")}}(router, handler) {
          if (router.fallback !== null) throw new Error("The router already has a fallback handler.");
          router.fallback = handler;
          return router;
        }
        function {{name("routerSegments")}}(pathname) {
          if (pathname === "/") return [];
          return pathname.slice(1).split("/").map(segment => {
            try {
              return decodeURIComponent(segment);
            } catch {
              return segment;
            }
          });
        }
        function {{name("routerMatch")}}(route, segments) {
          const parts = route.segments, parameters = Object.create(null);
          for (let index = 0; index < parts.length; index++) {
            const part = parts[index], value = segments[index];
            if (typeof part === "string") {
              if (value !== part) return null;
            } else if (part.rest === true) {
              parameters[part.name] = segments.slice(index).join("/");
              return parameters;
            } else {
              if (value === undefined || value === "" || (part.test !== undefined && !part.test(value))) return null;
              parameters[part.name] = value;
            }
          }
          return segments.length === parts.length ? parameters : null;
        }
        async function {{name("routerHandle")}}(router, request, env, context) {
          const segments = {{name("routerSegments")}}(new URL(request.url).pathname), method = request.method;
          let allowed = null;
          for (const entry of router.routes) {
            const parameters = {{name("routerMatch")}}(entry.route, segments);
            if (parameters === null) continue;
            const head = method === "HEAD" && entry.method === "GET";
            if (entry.method === null || entry.method === method || head) {
              const response = await entry.handler(request, { env, context, parameters });
              if (!head) return response;
              await response.body?.cancel();
              return new Response(null, response);
            }
            allowed ??= new Set();
            allowed.add(entry.method);
            if (entry.method === "GET") allowed.add("HEAD");
          }
          if (allowed !== null)
            return new Response("Method Not Allowed", { status: 405, headers: { allow: [...allowed].join(", ") } });
          if (router.fallback !== null)
            return await router.fallback(request, { env, context, parameters: Object.create(null) });
          return new Response("Not Found", { status: 404 });
        }
        function {{name("routerParameter")}}(route, name) {
          const value = route.parameters[name];
          if (value === undefined) throw new RangeError(`The matched route has no parameter named '${name}'.`);
          return value;
        }

        """;
}
