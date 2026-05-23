using MudBlazor;
using Nalix.Abstractions.Security;

namespace Nalix.Dashboard.Application.Navigation;

public static class AppNavigation
{
    public static IReadOnlyList<NavSection> Build(PermissionLevel currentLevel)
        =>
        [
            new NavSection("Dashboard",
            [
                new NavItem("Overview", "/", Icons.Material.Outlined.Dashboard, PermissionLevel.NONE)
            ]),
            new NavSection("Metrics",
            [
                new NavItem("Dispatch",        "/metrics/dispatch",        Icons.Material.Outlined.Hub,             PermissionLevel.SUPERVISOR),
                new NavItem("Tasks",           "/metrics/tasks",           Icons.Material.Outlined.Task,            PermissionLevel.SUPERVISOR),
                new NavItem("Buffers",         "/metrics/buffers",         Icons.Material.Outlined.Memory,          PermissionLevel.SUPERVISOR),
                new NavItem("Connections",     "/metrics/connections",     Icons.Material.Outlined.Cable,           PermissionLevel.SUPERVISOR),
                new NavItem("Instances",       "/metrics/instances",       Icons.Material.Outlined.AccountTree,     PermissionLevel.SUPERVISOR),
                new NavItem("Object Pools",    "/metrics/object-pools",    Icons.Material.Outlined.Pool,            PermissionLevel.SUPERVISOR),
                new NavItem("Connection Guard","/metrics/connection-guard", Icons.Material.Outlined.Shield,         PermissionLevel.SUPERVISOR),
                new NavItem("Listeners",       "/metrics/listeners",       Icons.Material.Outlined.Dns,             PermissionLevel.SUPERVISOR),
                new NavItem("Protocols",       "/metrics/protocols",       Icons.Material.Outlined.SwapHoriz,       PermissionLevel.SUPERVISOR),
                new NavItem("Sessions",        "/metrics/sessions",        Icons.Material.Outlined.VpnKey,          PermissionLevel.SUPERVISOR),
                new NavItem("Concurrency Gate","/metrics/concurrency-gate",Icons.Material.Outlined.Lock,             PermissionLevel.SUPERVISOR),
                new NavItem("Policy Rate Limiter","/metrics/policy-rate-limiter",Icons.Material.Outlined.Speed,        PermissionLevel.SUPERVISOR),
                new NavItem("Token Bucket Limiter","/metrics/token-bucket-limiter",Icons.Material.Outlined.HourglassEmpty,PermissionLevel.SUPERVISOR),
            ]),
            new NavSection("System",
            [
                new NavItem("Diagnostics", "/diagnostics", Icons.Material.Outlined.BugReport,  PermissionLevel.SUPERVISOR),
                new NavItem("Settings",    "/settings",    Icons.Material.Outlined.Settings,   PermissionLevel.NONE)
            ])
        ];

    public static bool IsVisible(NavItem item, PermissionLevel currentLevel)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsVisible && item.IsEnabled && currentLevel >= item.RequiredLevel;
    }
}
