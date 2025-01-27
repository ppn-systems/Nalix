namespace Notio.Web;

/// <summary>
/// Defines the HTTP listeners available for use in a <see cref="WebServer"/>.
/// </summary>
public enum HttpListenerMode
{
    /// <summary>
    /// Use Notio's internal HTTP listener implementation,
    /// based on Mono's <c>System.Net.HttpListener</c>.
    /// </summary>
    EmbedIO,

    /// <summary>
    /// Use the <see cref="System.Net.HttpListener"/> class
    /// provided by the .NET runtime in use.
    /// </summary>
    Microsoft,
}