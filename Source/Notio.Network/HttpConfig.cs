using Notio.Shared.Configuration;
using System;

namespace Notio.Network;

/// <summary>
/// Configuration settings for the HTTP server.
/// </summary>
public sealed class HttpConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets or sets the server's base URL.
    /// Default is "http://localhost:8080/".
    /// </summary>
    public string Prefixes { get; set; } = "http://localhost:8080/";

    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// Default is 100.
    /// </summary>
    public int MaxConcurrentRequests = 100;

    /* CorsOptions */

    /// <summary>
    /// Gets or sets the allowed origins for cross-origin requests.
    /// Default is ["*"].
    /// </summary>
    public string[] AllowedOrigins { get; set; } = ["*"];

    /// <summary>
    /// Gets or sets the allowed HTTP methods for cross-origin requests.
    /// Default is ["GET", "POST", "PUT", "DELETE", "OPTIONS"].
    /// </summary>
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];

    /// <summary>
    /// Gets or sets the allowed HTTP headers for cross-origin requests.
    /// Default is ["Content-Type", "Authorization"].
    /// </summary>
    public string[] AllowedHeaders { get; set; } = ["Content-Type", "Authorization"];

    /* RateLimitingOptions */

    /// <summary>
    /// Gets or sets the maximum number of requests allowed in a given window.
    /// Default is 100.
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Gets or sets the window duration (in minutes) for rate limiting.
    /// Default is 15 minutes.
    /// </summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// Gets the expiration time for cached responses.
    /// Default is 10 minutes.
    /// This property is ignored in the configuration binding process.
    /// </summary>
    [ConfigurationIgnore]
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
}