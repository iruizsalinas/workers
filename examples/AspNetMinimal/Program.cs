using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello from ASP.NET");
app.MapGet("/users/{id:int}", (int id, string? detail) =>
    Results.Ok(new { id, detail }));
app.MapPost("/users", (CreateUser user) =>
    Results.Created($"/users/{user.Name}", user));
app.MapDelete("/users/{id:int}", (int id) =>
    id == 1 ? Results.NoContent() : Results.NotFound(new { id }));
app.MapGet("/inspect/{id:int}", (
    [FromRoute] int id,
    [FromHeader(Name = "x-token")] string token,
    HttpRequest request) => Results.Ok(new { id, token, request.Method, request.Path }));
app.MapGet("/pages", (int page) => page);
app.MapGet("/health", Health);

app.Run();

static IResult Health() => Results.Ok(new { status = "healthy" });

internal sealed record CreateUser(string Name);
