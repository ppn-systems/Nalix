using Nalix.Dashboard.Application.Settings;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IAdminSettingsStore
{
    Task<AdminSettings> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AdminSettings settings, CancellationToken ct = default);

    Task<string?> GetApiKeyAsync(CancellationToken ct = default);

    Task SetApiKeyAsync(string apiKey, AdminSettings settings, CancellationToken ct = default);

    Task ClearApiKeyAsync(CancellationToken ct = default);
}
