using Microsoft.AspNetCore.Components;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.Reports;
using Nalix.Dashboard.Application.Settings;
using Nalix.Dashboard.Application.State;
using Nalix.Dashboard.Domain.Reports;
using Nalix.Observability.Contracts;

namespace Nalix.Dashboard.Components.Pages;

public abstract class MetricPageBase : ComponentBase, IDisposable
{
    [Inject] internal IDashboardStateReader State { get; set; } = null!;
    [Inject] internal IReportPollingController Polling { get; set; } = null!;
    [Inject] internal IAdminSettingsStore SettingsStore { get; set; } = null!;
    [Inject] internal IAdminClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Nav { get; set; } = null!;
    [Inject] internal ReportParserRegistry Parsers { get; set; } = null!;

    protected abstract RuntimeObservationTarget Target { get; }
    protected abstract string Route { get; }

    protected DashboardReportSnapshot? Snapshot { get; private set; }
    protected AdminSettings Settings { get; private set; } = new();
    protected bool IsLoading { get; private set; } = true;
    protected bool IsStale { get; private set; }

    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        if (!this.State.HasApiKey)
        {
            string? savedKey = await this.SettingsStore.GetApiKeyAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(savedKey))
            {
                await this.Client.SetApiKeyAsync(savedKey).ConfigureAwait(false);
            }
            else
            {
                this.Nav.NavigateTo("/login");
                return;
            }
        }

        this.Settings = await this.SettingsStore.LoadAsync().ConfigureAwait(false);
        int intervalMs = this.Settings.GetPollingInterval(this.Route);

        if (this.State.Reports.TryGetValue(this.Target, out DashboardReportSnapshot? cached))
        {
            this.Snapshot = cached;
            this.IsLoading = false;
        }

        this.Polling.Activate(this.Target, this.Route, intervalMs);
        this.Polling.Changed += this.OnPollingChanged;
        this.State.Changed += this.OnStateChanged;
    }

    private void OnStateChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (this.State.Reports.TryGetValue(this.Target, out DashboardReportSnapshot? snap))
        {
            this.Snapshot = snap;
            this.IsLoading = false;
        }

        this.UpdateStale();
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    private void OnPollingChanged()
    {
        if (_disposed)
        {
            return;
        }

        this.UpdateStale();
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    private void UpdateStale()
    {
        if (this.Polling.LastSuccessUtc is { } last)
        {
            double staleSecs = this.Settings.DefaultPollingIntervalMs / 1000.0 * 3;
            this.IsStale = (DateTimeOffset.UtcNow - last).TotalSeconds > staleSecs;
        }
    }

    protected void Pause() => this.Polling.Pause();
    protected void Resume() => this.Polling.Resume();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Errors are surfaced and displayed via polling state")]
    protected async Task ManualRefreshAsync()
    {
        try { await this.Polling.RequestOnceAsync(this.Target, CancellationToken.None).ConfigureAwait(false); }
        catch { /* surfaced via polling state */ }
    }

    protected void SetInterval(int ms) => this.Polling.SetInterval(ms);

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            this.Polling.Deactivate(this.Route);
            this.Polling.Changed -= this.OnPollingChanged;
            this.State.Changed -= this.OnStateChanged;
        }

        _disposed = true;
    }
}
