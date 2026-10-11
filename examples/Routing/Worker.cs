using Workers;

namespace Routing;

public static class Worker
{
    private static readonly Router Routes = new Router()
        .Use(async (request, route, next) => (await next()).WithHeader("x-router", "csharp"))
        .Get("/", (request, route) => Response.Text("Notes API"))
        .Get("/notes/{id:uuid}", GetNoteAsync)
        .Put("/notes/{id:uuid}", SaveNoteAsync)
        .Delete("/notes/{id:uuid}", DeleteNoteAsync)
        .Get("/files/{*path}", (request, route) => Response.Text($"File: {route.Parameter("path")}"))
        .Fallback((request, route) => Response.Json(new { error = "Not found" }, status: 404));

    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, Context context) =>
        Routes.HandleAsync(request, environment, context);

    private static async Task<Response> GetNoteAsync(Request request, RouteContext route)
    {
        var note = await route.Env.Kv("NOTES").GetJsonAsync<Note>(NoteKey(route));
        if (note is null) throw new HttpError(404, "Note not found");
        return Response.Json(note);
    }

    private static async Task<Response> SaveNoteAsync(Request request, RouteContext route)
    {
        var input = await request.JsonAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Text))
            throw new HttpError(400, "Text is required");

        var note = new Note(route.Parameter("id").ToLowerInvariant(), input.Text.Trim());
        await route.Env.Kv("NOTES").PutJsonAsync(NoteKey(route), note);
        return Response.Json(note);
    }

    private static async Task<Response> DeleteNoteAsync(Request request, RouteContext route)
    {
        await route.Env.Kv("NOTES").DeleteAsync(NoteKey(route));
        return Response.Empty(204);
    }

    // UUID paths use the canonical hyphenated format; keys normalize their case.
    private static string NoteKey(RouteContext route) => "note:" + route.Parameter("id").ToLowerInvariant();
}

public sealed record Note(string Id, string Text);

public sealed record NoteInput(string Text);
