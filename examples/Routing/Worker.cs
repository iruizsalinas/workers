using Workers;

namespace Routing;

public static class Worker
{
    private static readonly Router Routes = new Router()
        .Get("/", (request, route) => Response.Text("Notes API"))
        .Get("/notes/{id:guid}", GetNoteAsync)
        .Put("/notes/{id:guid}", SaveNoteAsync)
        .Delete("/notes/{id:guid}", DeleteNoteAsync)
        .Get("/files/{*path}", (request, route) => Response.Text($"File: {route.Parameter("path")}"))
        .Fallback((request, route) => Response.Json(new { error = "Not found" }, status: 404));

    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var response = await Routes.HandleAsync(request, environment, context);
        return response.WithHeader("x-router", "csharp");
    }

    private static async Task<Response> GetNoteAsync(Request request, RouteContext route)
    {
        var note = await route.Env.Kv("NOTES").GetJsonAsync<Note>(NoteKey(route));
        return note is null
            ? Response.Json(new { error = "Note not found" }, status: 404)
            : Response.Json(note);
    }

    private static async Task<Response> SaveNoteAsync(Request request, RouteContext route)
    {
        var input = await request.JsonAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Text))
            return Response.Json(new { error = "Text is required" }, status: 400);

        var note = new Note(Guid.Parse(route.Parameter("id")), input.Text.Trim());
        await route.Env.Kv("NOTES").PutJsonAsync(NoteKey(route), note);
        return Response.Json(note);
    }

    private static async Task<Response> DeleteNoteAsync(Request request, RouteContext route)
    {
        await route.Env.Kv("NOTES").DeleteAsync(NoteKey(route));
        return Response.Empty(204);
    }

    // The guid constraint accepts any format Guid.Parse does, so keys use its canonical form.
    private static string NoteKey(RouteContext route) => "note:" + Guid.Parse(route.Parameter("id"));
}

public sealed record Note(Guid Id, string Text);

public sealed record NoteInput(string Text);
