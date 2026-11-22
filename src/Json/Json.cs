using System.Text.Json;


namespace StepWise.Prose.Text.Json;

/// <summary>
/// Project-wide Json resources.
/// </summary>
public static class ProseJson {
    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}