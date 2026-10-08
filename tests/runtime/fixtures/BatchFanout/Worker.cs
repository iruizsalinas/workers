using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, Context context) =>
        request.Path == "/task-review"
            ? Response.Json(await TaskReviewScenarios.RunAsync())
            : Response.Json(await VerificationAsyncScenarios.RunAsync());
}
