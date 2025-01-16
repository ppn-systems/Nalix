using Notio.Http.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Notio.Http.Configuration;

/// <summary>
/// Interface for a cache of INotioClient instances.
/// </summary>
public interface IClientCache
{
    /// <summary>
    /// Adds a new INotioClient to this cache. Call once per client at startup to register and configure a named client.
    /// </summary>
    /// <param name="name">Name of the INotioClient. Serves as a cache key. Subsequent calls to Get will return this client.</param>
    /// <param name="baseUrl">Optional. The base URL associated with the new client.</param>
    /// <param name="configure">Optional. Configure the builder associated with the added client.</param>
    /// <returns>This IFlurlCache.</returns>
    IClientCache Add(string name, string baseUrl = null, Action<IClientBuilder> configure = null);

    /// <summary>
    /// Gets a preconfigured named INotioClient.
    /// </summary>
    /// <param name="name">The client name.</param>
    /// <returns>The cached INotioClient.</returns>
    INotioClient Get(string name);

    /// <summary>
    /// Gets a named INotioClient, creating and (optionally) configuring one if it doesn't exist or has been disposed.
    /// </summary>
    /// <param name="name">The client name.</param>
    /// <param name="baseUrl">The base URL associated with the new client, if it doesn't exist.</param>
    /// <param name="configure">Configure the builder associated with the new client, if it doesn't exist.</param>
    /// <returns>The cached INotioClient.</returns>
    INotioClient GetOrAdd(string name, string baseUrl = null, Action<IClientBuilder> configure = null);

    /// <summary>
    /// Adds initialization logic that gets executed for every new INotioClient added this cache.
    /// Good place for things like default settings. Executes before client-specific builder logic.
    /// Call at startup (or whenever the cache is first created); clients already cached will NOT have this logic applied.
    /// </summary>
    /// <returns>This IFlurlCache.</returns>
    IClientCache WithDefaults(Action<IClientBuilder> configure);

    /// <summary>
    /// Removes a named client from this cache.
    /// </summary>
    /// <returns>This IFlurlCache.</returns>
    IClientCache Remove(string name);

    /// <summary>
    /// Disposes and removes all cached INotioClient instances.
    /// </summary>
    /// <returns>This IFlurlCache.</returns>
    IClientCache Clear();
}

/// <summary>
/// Default implementation of IClientCache.
/// </summary>
public class ClientCache : IClientCache
{
    private readonly ConcurrentDictionary<string, Lazy<INotioClient>> _clients = new();
    private readonly List<Action<IClientBuilder>> _defaultConfigs = new();

    /// <inheritdoc />
    public IClientCache Add(string name, string baseUrl = null, Action<IClientBuilder> configure = null)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        var builder = CreateBuilder(baseUrl);
        if (!_clients.TryAdd(name, new Lazy<INotioClient>(builder.Build)))
            throw new ArgumentException($"A client named '{name}' was already registered. Add should be called just once per client at startup.");

        configure?.Invoke(builder);
        return this;
    }

    /// <inheritdoc />
    public virtual INotioClient Get(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        if (!_clients.TryGetValue(name, out var cli))
            throw new ArgumentException($"A client named '{name}' was not found. Either preconfigure the client using Add (typically at startup), or use GetOrAdd to add/configure one on demand when needed.");

        if (cli.Value.IsDisposed)
            throw new Exception($"A client named '{name}' was found but has been disposed and cannot be reused.");

        return cli.Value;
    }

    /// <inheritdoc />
    public INotioClient GetOrAdd(string name, string baseUrl = null, Action<IClientBuilder> configure = null)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        Lazy<INotioClient> Create()
        {
            var builder = CreateBuilder(baseUrl);
            configure?.Invoke(builder);
            return new Lazy<INotioClient>(builder.Build);
        }

        return _clients.AddOrUpdate(name, _ => Create(), (_, existing) => existing.Value.IsDisposed ? Create() : existing).Value;
    }

    /// <inheritdoc />
    public IClientCache WithDefaults(Action<IClientBuilder> configure)
    {
        if (configure != null)
            _defaultConfigs.Add(configure);
        return this;
    }

    /// <inheritdoc />
    public IClientCache Remove(string name)
    {
        if (_clients.TryRemove(name, out var cli) && cli.IsValueCreated && !cli.Value.IsDisposed)
            cli.Value.Dispose();
        return this;
    }

    /// <inheritdoc />
    public IClientCache Clear()
    {
        // Remove takes care of disposing too, which is why we don't simply call _clients.Clear
        foreach (var key in _clients.Keys)
            Remove(key);
        return this;
    }

    private IClientBuilder CreateBuilder(string baseUrl)
    {
        var builder = new ClientBuilder(baseUrl);
        foreach (var config in _defaultConfigs)
            config(builder);
        return builder;
    }
}