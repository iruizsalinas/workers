using Workers;

namespace RouterSemantics;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Path == "/conflict")
        {
            try
            {
                new Router()
                    .Get("/users/{id}", (request, route) => Response.Text("first"))
                    .Get("/users/{name}", (request, route) => Response.Text("second"));
                return Response.Text("registered");
            }
            catch (Exception exception)
            {
                return Response.Text(exception.Message, 500);
            }
        }

        var prefix = environment.Variable("PREFIX");
        return await new Router()
            .Get("/users/me", (request, route) => Response.Text($"{prefix}:me"))
            .Get("/users/{id:int}", (request, route) => Response.Text($"{prefix}:int:{int.Parse(route.Parameter("id")) + 1}"))
            .Get("/users/{id}", (request, route) => Response.Text($"{prefix}:name:{route.Parameter("id")}"))
            .Delete("/users/{id}", async (request, route) =>
            {
                await Task.Delay(1);
                return Response.Text($"deleted:{route.Parameter("id")}");
            })
            .Get("/items/{id:guid}", (request, route) => Response.Text(Guid.Parse(route.Parameter("id")).ToString()))
            .Any("/proxy/{*path}", (request, route) => Response.Text($"{request.Method}:{route.Parameter("path")}"))
            .Get("/proxy/status", (request, route) => Response.Text("status"))
            .Post("/echo", async (request, route) => Response.Text(await request.TextAsync(), 201))
            .Get("/", (request, route) => Response.Text("root"))
            .Fallback((request, route) => Response.Text($"fallback:{request.Path}", 404))
            .HandleAsync(request, environment, context);
    }
}
