using System.Text.Json;

namespace Notio.Defaults;

/// <summary>
/// Provides predefined JSON serialization settings for different use cases.
/// </summary>
public static class DefaultJsonOptions
{
    /// <summary>
    /// Standard JSON settings used across the application.
    /// Uses camelCase property naming and ignores null values.
    /// </summary>
    public static JsonSerializerOptions Standard => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// JSON settings optimized for HTTP requests and responses.
    /// Uses camelCase property naming and indented formatting for better readability.
    /// </summary>
    public static JsonSerializerOptions HttpFormatted => new()
    {
        WriteIndented = true, // Improves readability for debugging
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// JSON settings optimized for TCP communication.
    /// Uses camelCase property naming and no indentation to reduce data size.
    /// </summary>
    public static JsonSerializerOptions TcpCompact => new()
    {
        WriteIndented = false, // Optimized for compact payloads
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
