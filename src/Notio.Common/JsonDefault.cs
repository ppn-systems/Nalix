using System.Text.Json;

namespace Notio.Common;

/// <summary>
/// Provides predefined JSON serialization settings for different use cases.
/// </summary>
public static class JsonSettings
{
    /// <summary>
    /// JSON settings optimized for HTTP requests and responses.
    /// Uses camelCase property naming and indented formatting for better readability.
    /// </summary>
    public static JsonSerializerOptions Http => new()
    {
        WriteIndented = true, // Improves readability for debugging
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// JSON settings optimized for TCP communication.
    /// Uses camelCase property naming and no indentation to reduce data size.
    /// </summary>
    public static JsonSerializerOptions Tcp => new()
    {
        WriteIndented = false, // Optimized for compact payloads
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
