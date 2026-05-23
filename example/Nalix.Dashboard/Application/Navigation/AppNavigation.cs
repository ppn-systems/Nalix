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
