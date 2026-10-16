internal static partial class HelperSource
{
    private static string TypeOf(Func<string, string> name) => $$"""
        const {{name("typeDescriptors")}} = new Map();
        function {{name("typeOf")}}(key, create) {
          if (!{{name("typeDescriptors")}}.has(key)) {{name("typeDescriptors")}}.set(key, Object.freeze(create()));
          return {{name("typeDescriptors")}}.get(key);
        }

        """;
}
