using Notio.Shared.Configuration;
using System;

namespace Notio.Http;

/// <summary>
/// Configuration settings for the HTTP server.
/// </summary>
public sealed class HttpConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets the maximum number of concurrent requests the server can handle.
    /// Default is 100.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 100;

    /// <summary>
    /// Gets the maximum size of a request in bytes.
    /// Default is 100 MB.
    /// </summary>
    public int MaxRequestSize { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets a value indicating whether response compression is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether HTTPS is required for connections.
    /// Default is true.
    /// </summary>
    public bool RequireHttps { get; set; } = false;

    /// <summary>
    /// Gets the server's base URL.
    /// Default is "http://localhost".
    /// </summary>
    public string UniformResourceLocator { get; set; } = "http://localhost";

    /// <summary>
    /// Gets or sets the port number the server listens on.
    /// Default is 8080.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Gets the path to the SSL/TLS certificate file.
    /// Required if <see cref="RequireHttps"/> is set to true.
    /// </summary>
    public string CertPemFilePath { get; set; }

    /// <summary>
    /// Gets the password for the SSL/TLS certificate file.
    /// Required if <see cref="CertPemFilePath"/> is specified.
    /// </summary>
    public string CertificatePassword { get; set; }


    public string KeyPemFilePath { get; set; }

    /// <summary>
    /// Gets the expiration time for cached responses.
    /// Default is 10 minutes.
    /// This property is ignored in the configuration binding process.
    /// </summary>
    [ConfigurationIgnore]
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
}