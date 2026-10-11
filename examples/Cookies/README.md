# Cookies

[Worker.cs](./Worker.cs) reads a theme preference, writes it as a cookie, and deletes a session cookie on `/logout`. Follow the [running instructions](../README.md#running-an-example); this example needs no bindings.

Visit `/?theme=dark`, then `/` to read the stored preference. The query parameter takes precedence over the cookie. `/logout` redirects to `/` and expires `__Host-session`; the example checks for this cookie but does not create or authenticate sessions.

## Cookie APIs

`request.Cookies.Get(name)` reads the first cookie with that name, `GetAll(name)` reads duplicates, and `Contains(name)` checks for its presence. Values are percent-decoded on read.

`response.Cookies.Set(name, value, options)` appends a `Set-Cookie` header. `response.Cookies.Delete(name, options)` appends an expired cookie. Both preserve other cookies on the response.

Defaults are `HttpOnly`, `Secure`, `SameSite=Lax`, and `Path=/`. This example overrides `SameSite` to `Strict` and gives the theme cookie a one-year lifetime. Values are percent-encoded on write so they cannot inject attributes.

Invalid names, paths, domains, or prefix attributes throw. `SameSite=None` and `Partitioned` require `Secure`; `__Host-` cookies also require `Path=/` and no domain. `__Http-` prefixes require `HttpOnly`. The cookie name plus encoded value cannot exceed 4096 bytes.

When deleting a cookie, match its original path, domain, and prefix attributes, including the `Secure` setting.
