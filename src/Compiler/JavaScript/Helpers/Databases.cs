internal static partial class HelperSource
{
    // Database drivers expect Node.js Buffers for binary parameters.
    private static string SqlParameters(Func<string, string> name) => $$"""
        function {{name("sqlParameters")}}(values) {
          return values == null ? [] : Array.from(values, value => ArrayBuffer.isView(value) && typeof Buffer === "function" && !Buffer.isBuffer(value)
            ? Buffer.from(value.buffer, value.byteOffset, value.byteLength)
            : value);
        }

        """;

    // Documents are written as plain objects (records through toJSON, sets as arrays) and read back
    // with BSON wrappers unwrapped, so ObjectIds surface as hexadecimal strings.
    private static string MongoValues(Func<string, string> name) => $$"""
        function {{name("mongoDocument")}}(value, generateId = false) {
          if (value == null || typeof value !== "object" || value instanceof Date || ArrayBuffer.isView(value) || value._bsontype)
            return value;
          if (value instanceof Set) value = Array.from(value);
          if (Array.isArray(value)) return value.map(item => {{name("mongoDocument")}}(item));
          if (typeof value.toJSON === "function") value = value.toJSON();
          const result = {};
          for (const key of Object.keys(value))
            if (!(generateId && key === "_id" && value[key] == null)) result[key] = {{name("mongoDocument")}}(value[key]);
          return result;
        }
        function {{name("mongoValue")}}(value) {
          if (value == null || typeof value !== "object" || value instanceof Date || ArrayBuffer.isView(value)) return value;
          switch (value._bsontype) {
            case "ObjectId": return value.toHexString();
            case "Binary": return value.sub_type === 4 ? value.toUUID().toHexString(true) : value.buffer.slice(0, value.position);
            case "Long": return value.toNumber();
            case "Int32": case "Double": return value.valueOf();
            case "Decimal128": return value.toString();
          }
          if (Array.isArray(value)) return value.map({{name("mongoValue")}});
          const result = {};
          for (const key of Object.keys(value)) result[key] = {{name("mongoValue")}}(value[key]);
          return result;
        }

        """;
}
