using System.Text.Json;

Console.WriteLine(JsonSerializer.Serialize(CoreSemantics.Run(), new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));
