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
            return Task.FromResult(Response.Redirect("/").WithoutCookie("__Host-session"));

        var theme = request.QueryParameters.Get("theme") ?? request.Cookies.Get("theme") ?? "light";
        return Task.FromResult(
            Response.Json(new { theme, signedIn = request.Cookies.Contains("__Host-session") })
                .WithCookie("theme", theme, new CookieOptions { MaxAgeSeconds = OneYear, SameSite = SameSite.Strict }));
    }
}
