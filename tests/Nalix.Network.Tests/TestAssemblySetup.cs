using Nalix.Environment.Configuration;
using Nalix.Network.Options;

namespace Nalix.Network.Tests;

internal static class TestAssemblySetup
{
    internal static void EnsureHighLimits()
    {
        ConnectionQuotaOptions quota = ConfigurationManager.Instance.Get<ConnectionQuotaOptions>();
        quota.MaxConnectionsPerIpAddress = 10000;
        quota.MaxConnectionsPerWindow = 10000000;
    }
}