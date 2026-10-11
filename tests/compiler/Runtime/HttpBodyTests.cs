namespace Workers.Compiler.Tests;

public sealed class HttpBodyTests
{
    [Fact]
    public void UsesNativeBodyValuesAndResponseConsumptionApis()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var form = await request.FormDataAsync();
                    var response = new Response(form);
                    var streamResponse = new Response(request.Body!);
                    var queryResponse = new Response(request.QueryParameters);
                    var used = response.BodyUsed;
                    var parsed = await response.FormDataAsync();
                    return Response.Json(new { used, parsed, streamResponse, queryResponse });
                }
            }
            """);

        Assert.Contains("let response = new Response(form);", module);
        Assert.Contains("new Response(request.body)", module);
        Assert.Contains("new Response(new URL(request.url).searchParams)", module);
        Assert.Contains("response.bodyUsed", module);
        Assert.Contains("await response.formData()", module);
        Assert.DoesNotContain("fromFormData", module);
        Assert.DoesNotContain("fromStream", module);
        Assert.DoesNotContain("fromQueryParameters", module);
    }

    [Fact]
    public void BodyInputsExposeOnlyNativeConversions()
    {
        Assert.Empty(typeof(global::Workers.BodyInit).GetProperties());
        Assert.All(typeof(global::Workers.BodyInit).GetMethods()
            .Where(method => method.DeclaringType == typeof(global::Workers.BodyInit)),
            method => Assert.Equal("op_Implicit", method.Name));
    }
}
