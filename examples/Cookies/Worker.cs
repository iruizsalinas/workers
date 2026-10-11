using Workers;

namespace Cookies;

public static class Worker
{
    private const int OneYear = 60 * 60 * 24 * 365;

    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/logout")
        {
            var response = Response.Redirect("/");
            response.Cookies.Delete("__Host-session");
            return Task.FromResult(response);
        }

        var theme = request.QueryParameters.Get("theme") ?? request.Cookies.Get("theme") ?? "light";
        var result = Response.Json(new { theme, signedIn = request.Cookies.Contains("__Host-session") });
        result.Cookies.Set("theme", theme, new CookieOptions { MaxAgeSeconds = OneYear, SameSite = SameSite.Strict });
        return Task.FromResult(result);
    }
}
