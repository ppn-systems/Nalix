using Nalix.Abstractions.Security;

namespace Nalix.Dashboard.Application.Navigation;

public sealed record NavItem(
    string Title,
    string Route,
    string Icon,
    PermissionLevel RequiredLevel = PermissionLevel.NONE,
    bool IsEnabled = true,
    bool IsVisible = true,
    string? Badge = null);

public sealed record NavSection(string Title, IReadOnlyList<NavItem> Items);
