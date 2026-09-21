# Compatibility

Workers compiles a focused C# profile to native JavaScript. It does not include the CLR or the full .NET Base Class Library.

This is a quick overview, not a list of every overload. If something is unsupported, `dotnet publish` should report a `WRK` compiler error.

| Status | Meaning |
| --- | --- |
| 🟢 | Supported |
| 🔵 | Supported subset |
| 🔴 | Not supported |

## Cloudflare Workers APIs

| Area | Status |
| --- | :---: |
| Requests, responses, headers, bodies, forms, and streams | 🟢 |
| Fetch, scheduled, queue, email, and tail events; Durable Object alarms | 🟢 |
| KV, R2, D1, Cache, Hyperdrive, and Durable Objects | 🟢 |
| Queues, service bindings, RPC, and Worker entrypoints | 🟢 |
| WebSockets, TCP sockets, and HTML rewriting | 🟢 |
| Analytics Engine, AI, Images, Media, Vectorize, Workflows, and rate limiting | 🟢 |
| Secrets and version metadata | 🟢 |

The API follows Cloudflare's runtime closely. A few methods are left out when workerd has no matching behavior or a C# mapping would be misleading.

## C# language

| Feature | Status | Notes |
| --- | :---: | --- |
| Classes, records, constructors, fields, and instance methods | 🟢 | User types cannot use inheritance |
| Async methods, `await`, and iterators | 🟢 | Sync and async iterators are supported |
| Properties and object initializers | 🔵 | Auto, init, and getter-only computed properties |
| Exceptions and control flow | 🔵 | Common patterns are supported |
| Generics | 🔵 | Framework generics work; user-defined generic types do not |
| Inheritance, abstract types, and virtual dispatch | 🔴 | Generated Workers types are handled separately |
| Reflection, `dynamic`, and runtime code generation | 🔴 | These require a CLR runtime |

Durable Objects, Worker entrypoints, and HTML handlers have stricter class rules because they map directly to workerd exports.

## .NET APIs

| API | Status | Coverage |
| --- | :---: | --- |
| Console output | 🟢 | Uses the Workers console |
| `Task` and cancellation | 🔵 | Async operations, delays, tokens, and cancellation sources |
| Strings and `StringBuilder` | 🔵 | Common text operations |
| Numeric types and `Math` | 🔵 | Common arithmetic, parsing, formatting, and math functions |
| `DateTimeOffset`, `DateTime`, and `TimeSpan` | 🔵 | UTC-focused calendar, arithmetic, comparison, and conversion APIs |
| LINQ | 🔵 | Filtering, projection, ordering, grouping, joins, sets, and aggregation |
| Collections | 🔵 | Arrays, List, Dictionary, HashSet, Queue, and Stack |
| `System.Text.Json` | 🔵 | Native JSON conversion, `JsonElement`, and common attributes |
| `Guid` | 🔵 | Creation, parsing, comparison, and common formats |
| Threads, processes, filesystem APIs, and application domains | 🔴 | Not available in Workers |

Supporting a type does not mean every constructor, method, or overload is available. The compiler remains the exact source of truth.

## Contributing

Missing something useful? Feel free to open an issue or pull request for a .NET API, package profile, or Cloudflare feature. Small additions with a clean JavaScript or workerd mapping and good tests are welcome.
