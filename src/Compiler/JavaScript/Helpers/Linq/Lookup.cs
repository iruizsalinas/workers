internal static partial class HelperSource
{
    private static string LinqLookup(Func<string, string> name) => $$"""
        function {{name("linqLookup")}}(equals, hash) {
          const buckets = new Map(), entries = [];
          function find(key) {
            const code = hash(key), bucket = buckets.get(code);
            return { code, bucket, entry: bucket?.find(entry => equals(entry.key, key)) };
          }
          return {
            get size() { return entries.length; },
            get(key) { return find(key).entry?.value; },
            has(key) { return find(key).entry !== undefined; },
            set(key, value) {
              const found = find(key);
              if (found.entry) { found.entry.value = value; return; }
              const entry = { key, value };
              if (found.bucket) found.bucket.push(entry); else buckets.set(found.code, [entry]);
              entries.push(entry);
            },
            *values() { for (const entry of entries) yield entry.value; }
          };
        }

        """;
}
