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
                new NavItem("Dispatch",        "/metrics/dispatch",        Icons.Material.Outlined.Hub,             PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Tasks",           "/metrics/tasks",           Icons.Material.Outlined.Task,            PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Buffers",         "/metrics/buffers",         Icons.Material.Outlined.Memory,          PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Connections",     "/metrics/connections",     Icons.Material.Outlined.Cable,           PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Instances",       "/metrics/instances",       Icons.Material.Outlined.AccountTree,     PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Object Pools",    "/metrics/object-pools",    Icons.Material.Outlined.Pool,            PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Connection Guard","/metrics/connection-guard", Icons.Material.Outlined.Shield,         PermissionLevel.SYSTEM_ADMINISTRATOR),
            ]),
            new NavSection("System",
            [
                new NavItem("Diagnostics", "/diagnostics", Icons.Material.Outlined.BugReport,  PermissionLevel.SYSTEM_ADMINISTRATOR),
                new NavItem("Settings",    "/settings",    Icons.Material.Outlined.Settings,   PermissionLevel.NONE)
            ])
        ];

    public static bool IsVisible(NavItem item, PermissionLevel currentLevel)
        => item.IsVisible && item.IsEnabled && currentLevel >= item.RequiredLevel;
}
