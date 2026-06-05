// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;

using Nalix.Abstractions.Exceptions;
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

    private readonly TrustedProxyOptions _proxyConfig;
    private readonly ConnectionBlacklistStoreOptions _blacklistConfig;
    private volatile AccessListState _state;

    private sealed class AccessListState
    {
        public AccessListState(List<IPNetwork> trustedProxies, HashSet<IPAddress> blacklistedIps, List<IPNetwork> blacklistedNetworks)
        {
            this.TrustedProxies = trustedProxies;
            this.BlacklistedIps = blacklistedIps;
            this.BlacklistedNetworks = blacklistedNetworks;
        }

        public List<IPNetwork> TrustedProxies { get; }
        public HashSet<IPAddress> BlacklistedIps { get; }
        public List<IPNetwork> BlacklistedNetworks { get; }
    }

    public NetworkAccessList(TrustedProxyOptions proxyConfig)
    {
        _proxyConfig = proxyConfig;

        ConnectionBlacklistStoreOptions blacklistConfig = ConfigurationManager.Instance.Get<ConnectionBlacklistStoreOptions>();
        blacklistConfig.Validate();
        _blacklistConfig = blacklistConfig;

        List<IPNetwork> proxies = this.LoadTrustedProxies(proxyConfig);
        (HashSet<IPAddress> ips, List<IPNetwork> networks) = this.LoadBlacklistedIps(blacklistConfig);

        _state = new AccessListState(proxies, ips, networks);
    }

    #region APIs

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlacklisted(IPAddress address)
    {
        AccessListState state = _state;

        if (state.BlacklistedIps.Contains(address))
        {
            return true;
        }

        if (state.BlacklistedNetworks.Count > 0)
        {
            for (int i = 0; i < state.BlacklistedNetworks.Count; i++)
            {
                if (state.BlacklistedNetworks[i].Contains(address))
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
        AccessListState state = _state;

        if (state.TrustedProxies.Count > 0)
        {
            for (int i = 0; i < state.TrustedProxies.Count; i++)
            {
                if (state.TrustedProxies[i].Contains(address))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion APIs

    #region Loading Methods

    public void Reload()
    {
        try
        {
            List<IPNetwork>? proxies;
            HashSet<IPAddress>? ips;
            List<IPNetwork>? networks;

            try
            {
                proxies = this.LoadTrustedProxies(_proxyConfig);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                proxies = _state.TrustedProxies; // Retain old state on failure
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.AccessListState:Reload", "failed to reload trusted proxies, retaining old state.", ex));
                }
            }

            try
            {
                (ips, networks) = this.LoadBlacklistedIps(_blacklistConfig);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                ips = _state.BlacklistedIps; // Retain old state on failure
                networks = _state.BlacklistedNetworks;
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.AccessListState:Reload", "failed to reload blacklists, retaining old state.", ex));
                }
            }

            _state = new AccessListState(proxies, ips, networks);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.AccessListState:Reload", "unexpected error during reload.", ex));
            }
        }
    }

    private List<IPNetwork> LoadTrustedProxies(TrustedProxyOptions proxyConfig)
    {
        string path = Path.Combine(Directories.ConfigurationDirectory, proxyConfig.StoreFileName);
        if (!File.Exists(path))
        {
            NetworkStore.Save(path, Array.Empty<IPNetwork>(), "Trusted Proxies");
        }
        List<IPNetwork> networks = NetworkStore.Load(path, proxyConfig.MaxTrustedProxies);

        if (networks.Count > 0 && DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Information, new DiagnosticLog("NW.AccessListState:LoadTrustedProxies", $"Loaded networks-count={networks.Count} trusted proxies from disk."));
        }

        return networks;
    }

    private (HashSet<IPAddress>, List<IPNetwork>) LoadBlacklistedIps(ConnectionBlacklistStoreOptions blacklistConfig)
    {
        HashSet<IPAddress> ips = new();
        List<IPNetwork> netList = new();

        if (!blacklistConfig.Enabled)
        {
            return (ips, netList);
        }

        string path = Path.Combine(Directories.ConfigurationDirectory, blacklistConfig.StoreFileName);
        if (!File.Exists(path))
        {
            NetworkStore.Save(path, Array.Empty<IPNetwork>(), "Blacklisted IPs/Networks");
        }
        List<IPNetwork> networks = NetworkStore.Load(path, blacklistConfig.MaxBlacklistedIps);

        foreach (IPNetwork network in networks)
        {
            if ((network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && network.PrefixLength == 32) ||
                (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && network.PrefixLength == 128))
            {
                _ = ips.Add(network.BaseAddress);
            }
            else
            {
                netList.Add(network);
            }
        }

        if (networks.Count > 0 && DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Information, new DiagnosticLog("NW.AccessListState:LoadTrustedProxies", $"Loaded networks-count={networks.Count} blacklisted IP/networks from disk (single IPs: blacklisted-ips-count={ips.Count}, CIDR networks: blacklisted-networks-count={netList.Count})."));
        }

        return (ips, netList);
    }

    #endregion Loading Methods
}





