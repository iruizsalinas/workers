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

## C# language

| Feature | Status | Notes |
| --- | :---: | --- |
| Classes, records, constructors, fields, and instance methods | 🟢 | Records include value equality, `with`, and `ToString()` |
| Static fields, properties, and constructors | 🟢 | Initialized lazily on first use, like the CLR |
| Async methods, `await`, and iterators | 🟢 | Sync and async iterators are supported |
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
| LINQ | 🔵 | Filtering, projection, ordering, grouping, joins, sets, and aggregation |
| Collections | 🔵 | Arrays, List, Dictionary, HashSet, Queue, and Stack |
| `System.Text.Json` | 🔵 | Native JSON conversion, `JsonElement`, and common attributes |
| `Guid` | 🔵 | Creation, parsing, comparison, and common formats |
| Threads, processes, filesystem APIs, and application domains | 🔴 | Not available in Workers |

Supporting a type does not mean every constructor, method, or overload is available. The compiler remains the exact source of truth.

## Behavior notes

- **Culture.** Workers has no current culture. Culture-sensitive formatting such as `$"{price:N2}"` must be explicitly invariant: use `FormattableString.Invariant`, `string.Create(CultureInfo.InvariantCulture, ...)`, or an overload that takes `CultureInfo.InvariantCulture`. Round-trip date formats (`O`, `s`, `u`, `R`) work anywhere.
- **String ordering.** Ordering by a string key needs `StringComparer.Ordinal` or `StringComparer.OrdinalIgnoreCase`, because the .NET default comparison is culture-aware.
- **JSON contracts.** Workers APIs such as `Request.JsonAsync<T>`, `Response.Json`, KV JSON values, and query binding use web conventions: camelCase names, case-insensitive matching, and numbers given as strings. `JsonSerializer` keeps the .NET defaults: exact property names and strict number handling.
- **Dictionary order.** Dictionaries are JavaScript objects, so keys that look like array indexes (`"1"`, `"42"`) are enumerated first, in numeric order, followed by the other keys in insertion order.
- **RPC values.** Class and record instances passed to or returned from RPC methods arrive as plain data. Their properties are kept but their methods are not.

## Contributing

Missing something useful? Feel free to open an issue or pull request for a .NET API, package profile, or Cloudflare feature. Small additions with a clean JavaScript or workerd mapping and good tests are welcome.
