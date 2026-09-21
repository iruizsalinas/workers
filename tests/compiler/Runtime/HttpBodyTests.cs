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
                    var response = Response.FromBody(Body.FromFormData(form));
                    var streamResponse = Response.FromBody(Body.FromStream(request.BodyStream()!));
                    var queryResponse = Response.FromBody(Body.FromQueryParameters(request.QueryParameters));
                    var used = response.BodyUsed;
                    var parsed = await response.FormDataAsync();
                    return Response.Json(new { used, parsed, streamResponse, queryResponse });
                }
            }
            """);

        Assert.Contains("new Response($workers$body?.body ?? $workers$body)", module);
        Assert.Contains("response.bodyUsed", module);
        Assert.Contains("await response.formData()", module);
        Assert.DoesNotContain("fromFormData", module);
        Assert.DoesNotContain("fromStream", module);
        Assert.DoesNotContain("fromQueryParameters", module);
    }

    [Fact]
    public void ExposesOnlyOpaqueSynchronousBodyState()
    {
        Assert.Equal(
            ["Empty", "IsEmpty"],
            typeof(global::Workers.Body).GetProperties().Select(property => property.Name).Order().ToArray());
        Assert.Equal(
            ["FromBytes", "FromFormData", "FromQueryParameters", "FromStream", "Json", "Text"],
            typeof(global::Workers.Body).GetMethods()
                .Where(method => method.DeclaringType == typeof(global::Workers.Body) && !method.IsSpecialName)
                .Select(method => method.Name).Order().ToArray());
    }
}
