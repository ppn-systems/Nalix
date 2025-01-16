using System;
using Notio.Http.Configuration;
using Notio.Http.Interfaces;
using Notio.Http.Model;

namespace Notio.Http;

/// <summary>
/// A static object for configuring Flurl for "clientless" usage. Provides a default IClientCache instance primarily
/// for clientless support, but can be used directly, as an alternative to a DI-managed singleton cache.
/// </summary>
public static class NotioHttp
{
    private static Func<IRequest, string> _cachingStrategy = BuildClientNameByHost;

    /// <summary>
    /// A global collection of cached INotioClient instances.
    /// </summary>
    public static IClientCache Clients { get; } = new ClientCache();

    /// <summary>
    /// Gets a builder for configuring the INotioClient that would be selected for calling the given URL when the clientless pattern is used.
    /// Note that if you've overridden the caching strategy to vary clients by request properties other than Url, you should instead use
    /// NotioHttp.Clients.Add(name) to ensure you are configuring the correct client.
    /// </summary>
    public static IClientBuilder ConfigureClientForUrl(string url)
    {
        IClientBuilder builder = null;
        Clients.Add(_cachingStrategy(new Request(url)), null, b => builder = b);
        return builder;
    }

    /// <summary>
    /// Gets or creates the INotioClient that would be selected for sending the given IRequest when the clientless pattern is used.
    /// </summary>
    public static INotioClient GetClientForRequest(IRequest req) => Clients.GetOrAdd(_cachingStrategy(req));

    /// <summary>
    /// Sets a global caching strategy for getting or creating an INotioClient instance when the clientless pattern is used, e.g. url.GetAsync.
    /// </summary>
    /// <param name="buildClientName">A delegate that returns a cache key used to store and retrieve a client instance based on properties of the request.</param>
    public static void UseClientCachingStrategy(Func<IRequest, string> buildClientName) => _cachingStrategy = buildClientName;

    /// <summary>
    /// Sets a global caching strategy of one INotioClient per scheme/host/port combination when the clientless pattern is used,
    /// e.g. url.GetAsync. This is the default strategy, so you shouldn't need to call this except to revert a previous call to
    /// UseClientCachingStrategy, which would be rare.
    /// </summary>
    public static void UseClientPerHostStrategy() => _cachingStrategy = BuildClientNameByHost;

    /// <summary>
    /// Builds a cache key consisting of URL scheme, host, and port. This is the default client caching strategy.
    /// </summary>
    public static string BuildClientNameByHost(IRequest req) => $"{req.Url?.Scheme}|{req.Url?.Host}|{req.Url?.Port}";
}