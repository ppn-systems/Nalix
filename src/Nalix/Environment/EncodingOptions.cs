namespace Nalix.Environment;

/// <summary>
/// Predefined serialization settings.
/// </summary>

public static class EncodingOptions
{
    /// <summary>
    /// Default encoding for serialization and deserialization.
    /// </summary>
    public static System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;

    /// <summary>
    /// Standard JSON settings used across the application.
    /// Uses camelCase property naming and ignores null values.
    /// </summary>
    public static System.Text.Json.JsonSerializerOptions Standard => new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// JSON settings optimized for HTTP requests and responses.
    /// Uses camelCase property naming and indented formatting for better readability.
    /// </summary>
    public static System.Text.Json.JsonSerializerOptions HttpFormatted => new()
    {
        WriteIndented = true, // Improves readability for debugging
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// JSON settings optimized for Reliable communication.
    /// Uses camelCase property naming and no indentation to reduce data size.
    /// </summary>
    public static System.Text.Json.JsonSerializerOptions TcpCompact => new()
    {
        WriteIndented = false, // Optimized for compact payloads
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
