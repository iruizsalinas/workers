internal static partial class HelperSource
{
    // Parses a Cookie header into an immutable, ordered list. Duplicate names are kept because browsers
    // send one cookie per matching path, most specific first.
    private static string RequestCookies(Func<string, string> name) => $$"""
        class {{name("RequestCookies")}} {
          #entries = [];
          constructor(header) {
            for (const pair of (header ?? "").split(";")) {
              const separator = pair.indexOf("=");
              if (separator < 0) continue;
              const name = pair.slice(0, separator).trim();
              if (name === "") continue;
              let value = pair.slice(separator + 1).trim();
              if (value.length > 1 && value.startsWith("\"") && value.endsWith("\"")) value = value.slice(1, -1);
              this.#entries.push(Object.freeze({ name, value: {{name("cookieDecode")}}(value) }));
            }
          }
          get(name) { return this.#entries.find(entry => entry.name === name)?.value ?? null; }
          getAll(name) { return this.#entries.filter(entry => entry.name === name).map(entry => entry.value); }
          has(name) { return this.#entries.some(entry => entry.name === name); }
          [Symbol.iterator]() { return this.#entries.values(); }
        }
        function {{name("cookieDecode")}}(value) {
          if (!value.includes("%")) return value;
          try {
            return decodeURIComponent(value);
          } catch {
            return value;
          }
        }
        function {{name("requestCookies")}}(request) {
          return new {{name("RequestCookies")}}(request.headers.get("cookie"));
        }

        """;

    // Builds a Set-Cookie value. Values are percent-encoded so they can never inject attributes, and the
    // attribute rules browsers enforce silently are enforced here with an error instead.
    private static string SetCookie(Func<string, string> name) => $$"""
        const {{name("cookieToken")}} = /^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/;
        const {{name("cookiePath")}} = /^\/[\x21-\x3a\x3c-\x7e]*$/;
        const {{name("cookieDomain")}} = /^\.?[0-9A-Za-z](?:[0-9A-Za-z-]*[0-9A-Za-z])?(?:\.[0-9A-Za-z](?:[0-9A-Za-z-]*[0-9A-Za-z])?)*$/;
        const {{name("cookieSameSite")}} = ["Lax", "Strict", "None"];
        function {{name("setCookie")}}(name, value, options, expire = false) {
          const { path, domain, expires, maxAge, httpOnly, secure, sameSite, partitioned } =
            { path: "/", httpOnly: true, secure: true, sameSite: 0, partitioned: false, ...options };
          if (typeof name !== "string" || !{{name("cookieToken")}}.test(name))
            throw new TypeError(`Invalid cookie name ${JSON.stringify(name)}.`);
          if (typeof value !== "string") throw new TypeError("Cookie value cannot be null.");
          const encoded = encodeURIComponent(value);
          if (name.length + encoded.length > 4096)
            throw new RangeError(`Cookie '${name}' exceeds the 4096 byte limit browsers accept.`);
          const attributes = [`${name}=${encoded}`];
          if (path != null) {
            if (!{{name("cookiePath")}}.test(path)) throw new TypeError(`Invalid cookie path ${JSON.stringify(path)}.`);
            attributes.push(`Path=${path}`);
          }
          if (domain != null) {
            if (!{{name("cookieDomain")}}.test(domain)) throw new TypeError(`Invalid cookie domain ${JSON.stringify(domain)}.`);
            attributes.push(`Domain=${domain}`);
          }
          if (expire) {
            attributes.push("Max-Age=0", "Expires=Thu, 01 Jan 1970 00:00:00 GMT");
          } else {
            if (maxAge != null) {
              if (!Number.isSafeInteger(maxAge) || maxAge < 0)
                throw new RangeError("Cookie MaxAge must be a non-negative integer; use response.Cookies.Delete to delete a cookie.");
              attributes.push(`Max-Age=${maxAge}`);
            }
            if (expires != null) {
              if (Number.isNaN(expires.getTime())) throw new RangeError("Cookie Expires is not a valid date.");
              attributes.push(`Expires=${expires.toUTCString()}`);
            }
          }
          if (httpOnly) attributes.push("HttpOnly");
          if (secure) attributes.push("Secure");
          const site = {{name("cookieSameSite")}}[sameSite];
          if (site === undefined) throw new RangeError("Invalid cookie SameSite value.");
          attributes.push(`SameSite=${site}`);
          if (partitioned) attributes.push("Partitioned");
          const prefix = name.toLowerCase();
          if (!secure && (site === "None" || partitioned || prefix.startsWith("__secure-") || prefix.startsWith("__host-") || prefix.startsWith("__http-")))
            throw new TypeError(`Cookie '${name}' requires Secure.`);
          if (prefix.startsWith("__host-") && (domain != null || path !== "/"))
            throw new TypeError(`Cookie '${name}' requires Path=/ and no Domain.`);
          if (!httpOnly && (prefix.startsWith("__http-") || prefix.startsWith("__host-http-")))
            throw new TypeError(`Cookie '${name}' requires HttpOnly.`);
          return attributes.join("; ");
        }

        """;
}
