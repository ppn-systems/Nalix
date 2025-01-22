using Notio.Shared.Configuration;
using System;

namespace Notio.Network;

/// <summary>
/// Configuration settings for the HTTP server.
/// </summary>
public sealed class HttpConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets the server's base URL.
    /// Default is "http://localhost:8080/".
    /// </summary>
    public string Prefixes { get; set; } = "http://localhost:8080/";

    public int MaxConcurrentRequests = 100;

    /* CorsOptions */
    public string[] AllowedOrigins { get; set; } = ["*"];
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];
    public string[] AllowedHeaders { get; set; } = ["Content-Type", "Authorization"];

    /* RateLimitingOptions */
    public int MaxRequests { get; set; } = 100;
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// Gets the expiration time for cached responses.
    /// Default is 10 minutes.
    /// This property is ignored in the configuration binding process.
    /// </summary>
    [ConfigurationIgnore]
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
}