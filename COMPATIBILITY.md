# Compatibility

Workers compiles a focused C# profile to native JavaScript. It does not include the CLR or the full .NET Base Class Library.

This is a quick overview, not a list of every overload. If something is unsupported, `dotnet publish` should report a `WRK` compiler error.

| Status | Meaning |
| --- | --- |
| 🟢 | Supported |
| 🔵 | Supported subset |
| 🔴 | Not supported |

## Cloudflare Workers APIs

| Area | APIs | Status |
| --- | --- | :---: |
| HTTP | Requests, responses, headers, URLs, forms, bodies, fetch, and streams | 🟢 |
| Events | Fetch, scheduled, queue, email, and tail handlers | 🟢 |
| Storage | KV, R2, and Cache | 🟢 |
| Data | D1 and Hyperdrive | 🟢 |
| Databases | PostgreSQL, MySQL, and MongoDB clients mapped to `pg`, `mysql2`, and `mongodb` | 🔵 |
| Durable Objects | Bindings, storage, alarms, SQL, WebSockets, and containers | 🟢 |
| Messaging | Queues and email bindings | 🟢 |
| Services | Service bindings, dynamic dispatch, RPC, and Worker entrypoints | 🟢 |
| Networking | WebSockets and TCP sockets | 🟢 |
| Content | HTML rewriting, Images, and Media | 🟢 |
| AI and search | Workers AI and Vectorize | 🟢 |
| Workflows | Workflow bindings and instances | 🟢 |
| Analytics | Analytics Engine | 🟢 |
| Security | Rate limiting, secrets, and Web Crypto | 🟢 |
| Runtime | Context, timers, performance, text encoding, and version metadata | 🟢 |

The API follows Cloudflare's runtime closely. A few methods are left out when workerd has no matching behavior or a C# mapping would be misleading.

Cancellation uses `CancellationToken` and `CancellationTokenSource`. Incoming requests expose `Request.CancellationToken`; pass a token to the cancellation overloads of fetch and other operations. Native JavaScript abort controllers and signals are compiler implementation details.

URLs use `Workers.Url`; `System.Uri` is supported only for `EscapeDataString` and `UnescapeDataString`. Native fetch configuration beyond the typed options can be supplied through `FetchOptions.Cf` using an anonymous object, including image transformations.

Scheduled handlers use `ScheduledEvent.Cron` and `ScheduledTime`. Tail events are directly enumerable. WebSocket event streams use `socket.Events().NextAsync()`; Durable Object message callbacks use `WebSocketMessage.AsText()`.

## C# language

| Feature | Status | Notes |
| --- | :---: | --- |
| Classes, records, constructors, fields, and instance methods | 🟢 | Records include value equality, `with`, and `ToString()` |
| Static fields, properties, and constructors | 🟢 | Initialized lazily on first use, like the CLR |
| Async methods, `await`, and iterators | 🟢 | Sync and async iterators are supported |
| `using` and `await using` | 🔵 | Workers clients, `CancellationTokenSource`, and Worker types with a `Dispose` or `DisposeAsync` method |
| Properties and object initializers | 🔵 | Auto, init, and getter-only computed properties |
| Exceptions and control flow | 🔵 | Custom exceptions, typed `catch` clauses, and `when` filters |
| Pattern matching and `switch` expressions | 🔵 | Type, constant, relational, logical, property, positional, and list patterns |
| Casts, `out` arguments, and method groups | 🔵 | Numeric casts follow .NET rules; `out` works with the framework `Try...` methods |
| Generics | 🔵 | Framework generics work; user-defined generic types do not |
| Inheritance, abstract types, and virtual dispatch | 🔴 | Only exceptions may derive from `Exception`; generated Workers types are handled separately |
| Reflection, `dynamic`, and runtime code generation | 🔴 | These require a CLR runtime |

Durable Objects, Worker entrypoints, and HTML handlers have stricter class rules because they map directly to workerd exports.

## .NET APIs

| API | Status | Coverage |
| --- | :---: | --- |
| Console output | 🟢 | Uses the Workers console |
| `Task` and cancellation | 🔵 | Async operations, delays, tokens, and cancellation sources |
| Strings and `StringBuilder` | 🔵 | Common text operations |
| Numeric types and `Math` | 🔵 | Common arithmetic, parsing, invariant formatting, and math functions |
| `DateTimeOffset`, `DateTime`, and `TimeSpan` | 🔵 | UTC-focused calendar, arithmetic, comparison, and conversion APIs |
| `DateOnly` and `TimeOnly` | 🔵 | Calendar and clock arithmetic, comparison, invariant formatting, exact parsing, and JSON and database values |
| LINQ | 🔵 | Filtering, projection, ordering, grouping, joins, sets, and aggregation |
| Collections | 🔵 | Arrays, List, Dictionary, HashSet, Queue, and Stack |
| `System.Text.Json` | 🔵 | Native JSON conversion, `JsonElement`, `JsonNode`, and common attributes |
| Regular expressions | 🔵 | `Regex` and `[GeneratedRegex]` with constant patterns: matching, groups, replacement, and splitting |
| `Guid` | 🔵 | Creation, parsing, comparison, and common formats |
| Threads, processes, filesystem APIs, and application domains | 🔴 | Not available in Workers |

Supporting a type does not mean every constructor, method, or overload is available. The compiler remains the exact source of truth.

## Behavior notes

- **Culture.** Workers has no current culture. Culture-sensitive formatting such as `$"{price:N2}"` must be explicitly invariant: use `FormattableString.Invariant`, `string.Create(CultureInfo.InvariantCulture, ...)`, or an overload that takes `CultureInfo.InvariantCulture`. Round-trip date formats (`O`, `s`, `u`, `R`) work anywhere. Parse `DateOnly` and `TimeOnly` with `ParseExact` or `TryParseExact`, using `"O"` or a constant fixed-width format such as `"yyyy-MM-dd"` with `CultureInfo.InvariantCulture`.
- **String ordering.** Ordering by a string key needs `StringComparer.Ordinal` or `StringComparer.OrdinalIgnoreCase`, because the .NET default comparison is culture-aware.
- **JSON contracts.** Workers APIs such as `Request.JsonAsync<T>`, `Response.Json`, KV JSON values, and query binding use web conventions: camelCase names, case-insensitive matching, and numbers given as strings. `JsonSerializer` keeps the .NET defaults: exact property names and strict number handling.
- **JSON nodes.** `JsonObject`, `JsonArray`, and `JsonValue` are plain JSON values, so they pass directly to `Response.Json`, KV, and other Workers APIs. As in .NET, a node has one parent: adding a node that already has a parent, or one that would create a cycle, throws. Parsing allows a depth of 64. Numbers that a `double` cannot hold exactly, such as 64-bit IDs, keep their digits when written back as JSON. Other number text is normalized, so `1.0` is written as `1`. Such large numbers cannot be read with `GetValue<long>`, and trees that contain them cannot be stored or sent through structured clone, as with Durable Object storage or RPC. Values created in C# are read like parsed values, so `GetValue<double>()` works on a value created from an `int`. Duplicate property names keep the last value. `Parent`, `Root`, `GetPath`, `ReplaceWith`, and the reference-based `JsonArray.Remove`, `Contains`, and `IndexOf` are not available.
- **Dictionary order.** Dictionaries and `JsonObject` are JavaScript objects, so keys that look like array indexes (`"1"`, `"42"`) are enumerated first, in numeric order, followed by the other keys in insertion order.
- **Database rows.** Rows from D1, Durable Object SQL, and the database clients map to classes and records by case-insensitive column name. 64-bit and exact numerics returned as strings convert to numeric members when they fit, and binary columns convert to `byte[]`. MongoDB ObjectIds are read as hexadecimal strings: map `_id` with `[JsonPropertyName("_id")]` and filter with `MongoClient.ObjectId(id)`.
- **Database cancellation.** MongoDB reads (`FindAsync`, `FindOneAsync`, `CountDocumentsAsync`, `AggregateAsync`) are aborted when their token is canceled. PostgreSQL, MySQL, and MongoDB writes check the token before they start, because the drivers cannot abandon a statement in flight.
- **Regular expressions.** Patterns and options must be compile-time constants. They are translated to JavaScript when the Worker is built and keep .NET semantics for `\w`, `\d`, `\s`, `\b`, `^`, `$`, `.`, group numbering, empty matches, and replacement patterns. Constructs JavaScript cannot reproduce report `WRK120`: `RightToLeft`, `ECMAScript`, and `NonBacktracking` options, match timeouts, `\G`, conditionals, balancing groups, loops whose body can match empty text, and captures inside a loop that do not participate in every iteration. A surrogate pair matches as one character, so `.` matches a whole emoji where .NET matches each UTF-16 code unit.
- **RPC values.** Class and record instances passed to or returned from RPC methods arrive as plain data. Their properties are kept but their methods are not.

## Contributing

Missing something useful? Feel free to open an issue or pull request for a .NET API, package profile, or Cloudflare feature. Small additions with a clean JavaScript or workerd mapping and good tests are welcome.
