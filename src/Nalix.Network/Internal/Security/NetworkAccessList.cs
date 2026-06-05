// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Diagnostics;
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
            AccessListState currentState = _state;
            List<IPNetwork>? proxies;
            HashSet<IPAddress>? ips;
            List<IPNetwork>? networks;

            try
            {
                proxies = this.LoadTrustedProxies(_proxyConfig, currentState?.TrustedProxies);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                proxies = currentState.TrustedProxies; // Retain old state on failure
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.AccessListState:Reload", "reload-trusted-proxies-failed retaining-old-state=true", ex));
                }
            }

            try
            {
                (ips, networks) = this.LoadBlacklistedIps(_blacklistConfig, currentState?.BlacklistedIps, currentState?.BlacklistedNetworks);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                ips = currentState.BlacklistedIps; // Retain old state on failure
                networks = currentState.BlacklistedNetworks;
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.AccessListState:Reload", "reload-blacklists-failed retaining-old-state=true", ex));
                }
            }

            _state = new AccessListState(proxies, ips, networks);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.AccessListState:Reload", "reload-failed", ex));
            }
        }
    }

    private List<IPNetwork> LoadTrustedProxies(TrustedProxyOptions proxyConfig, List<IPNetwork>? existingProxies = null)
    {
        string path = Path.Combine(Directories.ConfigurationDirectory, proxyConfig.StoreFileName);
        if (!File.Exists(path))
        {
            NetworkStore.Save(path, Array.Empty<IPNetwork>(), "Trusted Proxies");
        }
        List<IPNetwork> networks = NetworkStore.Load(path, proxyConfig.MaxTrustedProxies);

        bool hasChanges = existingProxies == null || !AreNetworksEqual(networks, existingProxies);

        if (networks.Count > 0 && hasChanges && DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Information, new DiagnosticLog("NW.AccessListState:LoadTrustedProxies", $"loaded networks-count={networks.Count} source=disk"));
        }

        return networks;
    }

    private (HashSet<IPAddress>, List<IPNetwork>) LoadBlacklistedIps(ConnectionBlacklistStoreOptions blacklistConfig, HashSet<IPAddress>? existingIps = null, List<IPNetwork>? existingNetworks = null)
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

        bool hasChanges = existingIps == null || existingNetworks == null || !ips.SetEquals(existingIps) || !AreNetworksEqual(netList, existingNetworks);

        if (networks.Count > 0 && hasChanges && DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Information, new DiagnosticLog("NW.AccessListState:LoadTrustedProxies", $"loaded networks-count={networks.Count} blacklisted-ips-count={ips.Count} blacklisted-networks-count={netList.Count} source=disk"));
        }

        return (ips, netList);
    }

    private static bool AreNetworksEqual(List<IPNetwork> a, List<IPNetwork> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    #endregion Loading Methods
}





