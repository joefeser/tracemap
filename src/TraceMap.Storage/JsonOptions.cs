using System.Text.Json;

namespace TraceMap.Storage;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Stable = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions StableLine = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
