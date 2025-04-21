using Nalix.Threading;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Nalix.Network.Web.Security.Internal;

internal static class IPBanningExecutor
{
    private static readonly ConcurrentDictionary<string, IPBanningConfiguration> Configurations = new();

    static IPBanningExecutor()
    {
        Purger = new PeriodicTask(TimeSpan.FromMinutes(1), ct =>
        {
            foreach (string conf in Configurations.Keys)
            {
                if (Configurations.TryGetValue(conf, out IPBanningConfiguration? instance))
                {
                    instance.Purge();
                }
            }

            return Task.CompletedTask;
        });
    }

    public static readonly PeriodicTask Purger;

    public static IPBanningConfiguration RetrieveInstance(string baseRoute, int banMinutes)
    {
        return Configurations.GetOrAdd(baseRoute, x => new IPBanningConfiguration(banMinutes));
    }

    public static bool TryGetInstance(string baseRoute, out IPBanningConfiguration? configuration)
    {
        return Configurations.TryGetValue(baseRoute, out configuration);
    }

    public static bool TryRemoveInstance(string baseRoute)
    {
        return Configurations.TryRemove(baseRoute, out _);
    }
}