using System.Text.Json;
using Microsoft.JSInterop;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.Settings;

namespace Nalix.Dashboard.Infrastructure.BrowserStorage;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Browser storage access may fail due to sandboxing, private browsing, or quota exceptions, which should not crash the dashboard.")]
internal sealed class BrowserAdminSettingsStore : IAdminSettingsStore
{
    private const string SettingsKey = "nalix-admin-settings";
    private const string ApiKeyKey = "nalix-admin-apikey";

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IJSRuntime _js;
    private AdminSettings? _cached;

    public BrowserAdminSettingsStore(IJSRuntime js) => _js = js;

    public async Task<AdminSettings> LoadAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            string? json = await _js.InvokeAsync<string?>("adminStorage.getLocal", ct, SettingsKey)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(json))
            {
                _cached = JsonSerializer.Deserialize<AdminSettings>(json, s_json) ?? new AdminSettings();
                return _cached;
            }
        }
        catch
        {
            // Storage unavailable — return defaults.
        }

        _cached = new AdminSettings();
        return _cached;
    }

    public async Task SaveAsync(AdminSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _cached = settings;

        try
        {
            string json = JsonSerializer.Serialize(settings, s_json);
            await _js.InvokeVoidAsync("adminStorage.setLocal", ct, SettingsKey, json).ConfigureAwait(false);
        }
        catch
        {
            // Storage unavailable — settings not persisted.
        }
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            string? key = await _js.InvokeAsync<string?>("adminStorage.getSession", ct, ApiKeyKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
            return await _js.InvokeAsync<string?>("adminStorage.getLocal", ct, ApiKeyKey).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetApiKeyAsync(string apiKey, AdminSettings settings, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("adminStorage.removeSession", ct, ApiKeyKey).ConfigureAwait(false);
            await _js.InvokeVoidAsync("adminStorage.removeLocal", ct, ApiKeyKey).ConfigureAwait(false);
        }
        catch
        {
            // Best effort
        }

        try
        {
            if (settings.SaveKeyForNextTime)
            {
                await _js.InvokeVoidAsync("adminStorage.setLocal", ct, ApiKeyKey, apiKey).ConfigureAwait(false);
            }
            else
            {
                await _js.InvokeVoidAsync("adminStorage.setSession", ct, ApiKeyKey, apiKey).ConfigureAwait(false);
            }
        }
        catch
        {
            // Storage unavailable.
        }
    }

    public async Task ClearApiKeyAsync(CancellationToken ct = default)
    {
        _cached = null;
        try
        {
            await _js.InvokeVoidAsync("adminStorage.removeSession", ct, ApiKeyKey).ConfigureAwait(false);
            await _js.InvokeVoidAsync("adminStorage.removeLocal", ct, ApiKeyKey).ConfigureAwait(false);
            await _js.InvokeVoidAsync("adminStorage.clearSession", ct).ConfigureAwait(false);
        }
        catch
        {
            // Storage unavailable.
        }
    }
}
