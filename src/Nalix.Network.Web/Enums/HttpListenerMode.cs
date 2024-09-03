namespace Nalix.Network.Web.Enums;

/// <summary>
/// Defines the HTTP listeners available for use in a <see cref="WebServer"/>.
/// </summary>
public enum HttpListenerMode
{
    /// <summary>
    /// Use Notio's internal HTTP listener implementation,
    /// based on Mono's <c>System.Clients.HttpListener</c>.
    /// </summary>
    Notio,

    /// <summary>
    /// Use the <see cref="System.Net.HttpListener"/> class
    /// provided by the .NET runtime in use.
    /// </summary>
    Microsoft,
}