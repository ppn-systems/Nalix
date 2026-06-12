using Nalix.Environment.Configuration;
using Nalix.Network.Options;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, MaxParallelThreads = 1)]

namespace Nalix.SDK.Tests;

internal static class TestAssemblySetup
{
    /// <summary>
    /// Override connection limits after Bootstrap has switched to server.ini.
    /// Call this from each test class constructor.
    /// </summary>
    internal static void EnsureHighLimits()
    {
        // Update cached config object in-memory only.
        // Do NOT write to INI or flush — FileSystemWatcher would trigger reload.
        ConnectionQuotaOptions quota = ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();
        quota.MaxConnectionsPerIpAddress = 10000;
        quota.MaxConnectionsPerWindow = 10000000;
    }
}