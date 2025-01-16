using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Notio.Http.Configuration;

/// <summary>
/// Interface for helper methods used to construct INotioClient instances.
/// </summary>
public interface IClientFactory
{
    /// <summary>
    /// Creates and configures a new HttpClient as needed when a new INotioClient instance is created.
    /// Implementors should NOT attempt to cache or reuse HttpClient instances here - their lifetime is
    /// bound one-to-one with an INotioClient, whose caching and reuse is managed by IClientCache.
    /// </summary>
    /// <param name="handler">The HttpMessageHandler passed to the constructor of the HttpClient.</param>
    HttpClient CreateHttpClient(HttpMessageHandler handler);

    /// <summary>
    /// Creates and configures a new HttpMessageHandler as needed when a new INotioClient instance is created.
    /// The default implementation creates an instance of SocketsHttpHandler for platforms that support it,
    /// otherwise HttpClientHandler.
    /// </summary>
    HttpMessageHandler CreateInnerHandler();
}

/// <summary>
/// Extension methods on IClientFactory
/// </summary>
public static class ClientFactoryExtensions
{
    /// <summary>
    /// Creates an HttpClient with the HttpMessageHandler returned from this factory's CreateInnerHandler method.
    /// </summary>
    public static HttpClient CreateHttpClient(this IClientFactory fac) => fac.CreateHttpClient(fac.CreateInnerHandler());
}

/// <summary>
/// Default implementation of IClientFactory, used to build and cache INotioClient instances.
/// </summary>
public class DefaultClientFactory : IClientFactory
{
    // cached Blazor/WASM check (#543, #823)
    private readonly bool _isBrowser =
#if NET
        OperatingSystem.IsBrowser();
#else
		false;
#endif

    /// <inheritdoc />
    public virtual HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler);
    }

    /// <summary>
    /// Creates and configures a new HttpMessageHandler as needed when a new INotioClient instance is created.
    /// </summary>
    public virtual HttpMessageHandler CreateInnerHandler()
    {
        // Flurl has its own mechanisms for managing cookies and redirects, so we need to disable them in the inner handler.
        var handler = new HttpClientHandler();

        if (handler.SupportsRedirectConfiguration)
            handler.AllowAutoRedirect = false;

        // #266
        // deflate not working? see #474
        if (handler.SupportsAutomaticDecompression)
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

        if (!_isBrowser)
        {
            try { handler.UseCookies = false; }
            catch (PlatformNotSupportedException) { } // already checked for Blazor, but just in case any other surprises pop up
        }

        return handler;
    }
}

#if NET
/// <summary>
/// An implementation of IClientFactory that uses SocketsHttpHandler on supported platforms.
/// </summary>
public class SocketsHandlerClientFactory : DefaultClientFactory
{
    /// <summary>
    /// Creates and configures a new SocketsHttpHandler as needed when a new INotioClient instance is created.
    /// </summary>
    public override HttpMessageHandler CreateInnerHandler() => new SocketsHttpHandler
    {
        // Flurl has its own mechanisms for managing cookies and redirects, so we need to disable them in the inner handler.
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };
}
#endif
