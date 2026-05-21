// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Environment.Configuration;
using Nalix.Environment.IO;
using Nalix.Network.Options;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Encapsulates blacklist loading and trusted proxy checking for connection filtering.
/// </summary>
internal sealed class NetworkAccessList
{
    private readonly ILogger? _logger;
    private readonly List<IPNetwork> _trustedProxies = new();
    private readonly HashSet<IPAddress> _blacklistedIps = new();
    private readonly List<IPNetwork> _blacklistedNetworks = new();

    public NetworkAccessList(ILogger? logger, TrustedProxyOptions proxyConfig)
    {
        _logger = logger;

        this.LoadTrustedProxies(proxyConfig);

        ConnectionBlacklistStoreOptions blacklistConfig = ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>();
        blacklistConfig.Validate();
        this.LoadBlacklistedIps(blacklistConfig);
    }

    #region APIs

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlacklisted(IPAddress address)
    {
        if (_blacklistedIps.Contains(address))
        {
            return true;
        }

        if (_blacklistedNetworks.Count > 0)
        {
            for (int i = 0; i < _blacklistedNetworks.Count; i++)
            {
                if (_blacklistedNetworks[i].Contains(address))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTrustedProxy(IPAddress address)
    {
        if (_trustedProxies.Count > 0)
        {
            for (int i = 0; i < _trustedProxies.Count; i++)
            {
                if (_trustedProxies[i].Contains(address))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion APIs

    #region Loading Methods

    private void LoadTrustedProxies(TrustedProxyOptions proxyConfig)
    {
        string path = Path.Combine(Directories.ConfigurationDirectory, proxyConfig.StoreFileName);
        if (!File.Exists(path))
        {
            NetworkStore.Save(path, System.Array.Empty<IPNetwork>(), "Trusted Proxies");
        }
        List<IPNetwork> networks = NetworkStore.Load(path, proxyConfig.MaxTrustedProxies);

        foreach (IPNetwork network in networks)
        {
            _trustedProxies.Add(network);
        }

        if (networks.Count > 0 && _logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[NW.NetworkAccessList] Loaded {networks.Count} trusted proxies from disk.");
        }
    }

    private void LoadBlacklistedIps(ConnectionBlacklistStoreOptions blacklistConfig)
    {
        if (!blacklistConfig.Enabled)
        {
            return;
        }

        string path = Path.Combine(Directories.ConfigurationDirectory, blacklistConfig.StoreFileName);
        if (!File.Exists(path))
        {
            NetworkStore.Save(path, System.Array.Empty<IPNetwork>(), "Blacklisted IPs/Networks");
        }
        List<IPNetwork> networks = NetworkStore.Load(path, blacklistConfig.MaxBlacklistedIps);

        foreach (IPNetwork network in networks)
        {
            if ((network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && network.PrefixLength == 32) ||
                (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && network.PrefixLength == 128))
            {
                _ = _blacklistedIps.Add(network.BaseAddress);
            }
            else
            {
                _blacklistedNetworks.Add(network);
            }
        }

        if (networks.Count > 0 && _logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[NW.NetworkAccessList] Loaded {networks.Count} blacklisted IP/networks from disk (single IPs: {_blacklistedIps.Count}, CIDR networks: {_blacklistedNetworks.Count}).");
        }
    }

    #endregion Loading Methods
}
