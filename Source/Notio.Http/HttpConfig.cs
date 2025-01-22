using Notio.Shared.Configuration;
using System;

namespace Notio.Http;

/// <summary>
/// Configuration settings for the HTTP server.
/// </summary>
public sealed class HttpConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets the server's base URL.
    /// Default is "http://localhost:8080/".
    /// </summary>
    public string UniformResourceLocator { get; set; } = "http://localhost:8080/";

    /// <summary>
    /// Gets the expiration time for cached responses.
    /// Default is 10 minutes.
    /// This property is ignored in the configuration binding process.
    /// </summary>
    [ConfigurationIgnore]
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
}