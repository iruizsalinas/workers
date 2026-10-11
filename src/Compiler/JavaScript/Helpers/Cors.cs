internal static partial class HelperSource
{
    private static string Cors(Func<string, string> name) => $$"""
        function {{name("corsAllow")}}(origins, methods, credentials = false, headers, exposedHeaders, maxAge = 0) {
          const invalidToken = /[^!#$%&'*+.^_`|~0-9A-Za-z-]/;
          const tokens = (values, label, lower) => {
            if (!Array.isArray(values) || values.some(value => typeof value !== "string" || !value.length || invalidToken.test(value) || value === "*"))
              throw new TypeError(`Invalid CORS ${label}. Use explicit tokens.`);
            return [...new Set(values.map(value => lower ? value.toLowerCase() : value))];
          };
          if (!Array.isArray(origins) || origins.length === 0 || origins.some(origin => {
            if (origin === "*") return false;
            if (typeof origin !== "string" || origin === "null") return true;
            try {
              const url = new URL(origin);
              return !["http:", "https:"].includes(url.protocol) || url.origin !== origin;
            } catch { return true; }
          })) throw new TypeError("CORS origins must be exact HTTP(S) origins or *.");
          const allowedOrigins = new Set(origins), wildcard = allowedOrigins.has("*");
          if (wildcard && credentials) throw new TypeError("Wildcard CORS origins cannot allow credentials.");
          const allowedMethods = tokens(methods ?? ["GET", "HEAD", "POST"], "methods", false);
          const allowedHeaders = tokens(headers ?? [], "headers", true);
          const exposed = tokens(exposedHeaders ?? [], "exposed headers", false);
          if (!Number.isInteger(maxAge) || maxAge < 0) throw new RangeError("CORS maxAge must be a non-negative integer.");
          const vary = (response, values) => {
            const existing = (response.headers.get("vary") ?? "").split(",").map(value => value.trim()).filter(Boolean);
            if (!existing.includes("*"))
              for (const value of values)
                if (!existing.some(item => item.toLowerCase() === value.toLowerCase())) existing.push(value);
            response.headers.set("vary", existing.join(", "));
            return response;
          };
          return async (request, route, next) => {
            const origin = request.headers.get("origin");
            const requestedMethod = request.headers.get("access-control-request-method");
            const preflight = request.method === "OPTIONS" && origin !== null && requestedMethod !== null;
            // Never grant opaque/null or malformed origins, even with a wildcard policy.
            let validOrigin = false;
            try { validOrigin = origin !== null && new URL(origin).origin === origin && /^https?:/.test(origin); } catch {}
            const allowed = validOrigin && (wildcard || allowedOrigins.has(origin));
            let response;
            if (preflight) {
              const rawHeaders = request.headers.get("access-control-request-headers");
              const requestedHeaders = rawHeaders === null ? [] : rawHeaders.split(",").map(value => value.trim().toLowerCase());
              const accepted = allowed && allowedMethods.includes(requestedMethod)
                && requestedHeaders.every(value => value.length > 0 && !invalidToken.test(value) && allowedHeaders.includes(value));
              response = new Response(null, { status: accepted ? 204 : 403 });
              response = vary(response, ["Origin", "Access-Control-Request-Method", "Access-Control-Request-Headers"]);
              if (!accepted) return response;
              response.headers.set("access-control-allow-methods", allowedMethods.join(", "));
              if (allowedHeaders.length) response.headers.set("access-control-allow-headers", allowedHeaders.join(", "));
              if (maxAge > 0) response.headers.set("access-control-max-age", String(maxAge));
            } else {
              response = await next();
              response = new Response(response.body, response);
            }
            // Clone response headers without buffering streams; overwrite downstream CORS grants.
            response.headers.delete("access-control-allow-origin");
            for (const header of ["access-control-allow-credentials", "access-control-expose-headers", "access-control-allow-methods", "access-control-allow-headers", "access-control-max-age"])
              if (!preflight) response.headers.delete(header);
            response = vary(response, ["Origin"]);
            if (!allowed) return response;
            response.headers.set("access-control-allow-origin", wildcard ? "*" : origin);
            if (credentials) response.headers.set("access-control-allow-credentials", "true");
            if (!preflight && exposed.length) response.headers.set("access-control-expose-headers", exposed.join(", "));
            return response;
          };
        }

        """;
}
