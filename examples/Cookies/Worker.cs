using Workers;

namespace Cookies;

public static class Worker
{
    private const int OneYear = 60 * 60 * 24 * 365;

    [Fetch]
    public static Task<Response> FetchAsync(Request request)
    {
        var url = new Url(request.Url);
        if (url.Pathname == "/logout")
        {
            var response = Response.Redirect("/");
            response.Cookies.Delete("__Host-session");
            return Task.FromResult(response);
        }

        var theme = url.SearchParams.Get("theme") ?? request.Cookies.Get("theme") ?? "light";
        var result = Response.Json(new { theme, signedIn = request.Cookies.Has("__Host-session") });
        result.Cookies.Set("theme", theme, new CookieOptions { MaxAge = OneYear, SameSite = SameSite.Strict });
        return Task.FromResult(result);
    }
}
