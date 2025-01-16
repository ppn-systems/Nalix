using Notio.Http.Enums;
using Notio.Http.Exceptions;
using Notio.Http.Interfaces;
using Notio.Http.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;

namespace Notio.Http.Configuration;

/// <summary>
/// A builder for configuring INotioClient instances.
/// </summary>
public interface IClientBuilder : ISettingsContainer, IHeadersContainer, IEventHandlerContainer
{
    /// <summary>
    /// Configure the HttpClient wrapped by this INotioClient.
    /// </summary>
    IClientBuilder ConfigureHttpClient(Action<HttpClient> configure);

    /// <summary>
    /// Configure the inner-most HttpMessageHandler (an instance of HttpClientHandler) associated with this INotioClient.
    /// </summary>
    IClientBuilder ConfigureInnerHandler(Action<HttpClientHandler> configure);

#if NET

    /// <summary>
    /// Configure a SocketsHttpHandler instead of HttpClientHandler as the inner-most HttpMessageHandler.
    /// Note that HttpClientHandler has broader platform support and defers its work to SocketsHttpHandler
    /// on supported platforms. It is recommended to explicitly use SocketsHttpHandler ONLY if you
    /// need to directly configure its properties that aren't available on HttpClientHandler.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    IClientBuilder UseSocketsHttpHandler(Action<SocketsHttpHandler> configure);

#endif

    /// <summary>
    /// Add a provided DelegatingHandler to the INotioClient.
    /// </summary>
    IClientBuilder AddMiddleware(Func<DelegatingHandler> create);

    /// <summary>
    /// Builds an instance of INotioClient based on configurations specified.
    /// </summary>
    INotioClient Build();
}

/// <summary>
/// Default implementation of IClientBuilder.
/// </summary>
public class ClientBuilder : IClientBuilder
{
    private IClientFactory _factory = new DefaultClientFactory();

    private readonly string _baseUrl;
    private readonly List<Func<DelegatingHandler>> _addMiddleware = new();
    private readonly List<Action<HttpClient>> _clientConfigs = new();
    private readonly List<Action<HttpMessageHandler>> _handlerConfigs = new();

    /// <inheritdoc />
    public HttpSettings Settings { get; } = new();

    /// <inheritdoc />
    public IList<(HttpEventType, INotioEventHandler)> EventHandlers { get; } = new List<(HttpEventType, INotioEventHandler)>();

    /// <inheritdoc />
    public INameValueList<string> Headers { get; } = new NameValueList<string>(false); // header names are case-insensitive https://stackoverflow.com/a/5259004/62600

    /// <summary>
    /// Creates a new ClientBuilder.
    /// </summary>
    public ClientBuilder(string baseUrl = null)
    {
        _baseUrl = baseUrl;
    }

    /// <inheritdoc />
    public IClientBuilder AddMiddleware(Func<DelegatingHandler> create)
    {
        _addMiddleware.Add(create);
        return this;
    }

    /// <inheritdoc />
    public IClientBuilder ConfigureHttpClient(Action<HttpClient> configure)
    {
        _clientConfigs.Add(configure);
        return this;
    }

    /// <inheritdoc />
    public IClientBuilder ConfigureInnerHandler(Action<HttpClientHandler> configure)
    {
#if NET
        if (_factory is SocketsHandlerClientFactory && _handlerConfigs.Any())
            throw new ConfigurationException("ConfigureInnerHandler and UseSocketsHttpHandler cannot be used together. The former configures and instance of HttpClientHandler and would be ignored when switching to SocketsHttpHandler.");
#endif
        _handlerConfigs.Add(h => configure(h as HttpClientHandler));
        return this;
    }

#if NET

    /// <inheritdoc />
    public IClientBuilder UseSocketsHttpHandler(Action<SocketsHttpHandler> configure)
    {
        if (!SocketsHttpHandler.IsSupported)
            throw new PlatformNotSupportedException("SocketsHttpHandler is not supported on one or more target platforms.");

        if (_factory is DefaultClientFactory && _handlerConfigs.Any())
            throw new ConfigurationException("ConfigureInnerHandler and UseSocketsHttpHandler cannot be used together. The former configures and instance of HttpClientHandler and would be ignored when switching to SocketsHttpHandler.");

        if (_factory is not SocketsHandlerClientFactory)
            _factory = new SocketsHandlerClientFactory();

        _handlerConfigs.Add(h => configure(h as SocketsHttpHandler));
        return this;
    }

#endif

    /// <inheritdoc />
    public INotioClient Build()
    {
        var outerHandler = _factory.CreateInnerHandler();
        foreach (var config in _handlerConfigs)
            config(outerHandler);

        foreach (var middleware in Enumerable.Reverse(_addMiddleware).Select(create => create()))
        {
            middleware.InnerHandler = outerHandler;
            outerHandler = middleware;
        }

        var httpCli = _factory.CreateHttpClient(outerHandler);
        foreach (var config in _clientConfigs)
            config(httpCli);

        return new NotioClient(httpCli, _baseUrl, Settings, Headers, EventHandlers);
    }
}