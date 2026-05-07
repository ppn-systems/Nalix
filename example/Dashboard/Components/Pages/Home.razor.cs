using Contracts;
using Dashboard.Application.Reports;
using Dashboard.Domain.Reports;

namespace Dashboard.Components.Pages;

public sealed partial class Home : IDisposable
{
    private static IReadOnlyList<GenerationReportTarget> Targets => DashboardReportTargets.All;

    private string? _apiKey;
    private int _activePanelIndex;
    private bool _refreshing;
    private bool _savingKey;

    private IReadOnlyDictionary<GenerationReportTarget, DashboardReportSnapshot> Reports => State.Reports;

    private GenerationReportTarget? SelectedReportTarget => DashboardReportTargets.Resolve(_activePanelIndex);

    private string ReportShellClass => State.IsReportNavigationOpen ? "report-shell" : "report-shell drawer-closed";

    protected override void OnInitialized()
    {
        _activePanelIndex = State.ActiveReportTarget is { } target
            ? Math.Max(0, DashboardReportTargets.IndexOf(target))
            : DashboardReportTargets.Count;
        State.Changed += OnStateChanged;
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task SaveApiKey()
    {
        _savingKey = true;
        try
        {
            await Client.SetApiKeyAsync(_apiKey ?? string.Empty).ConfigureAwait(false);
        }
        finally
        {
            _savingKey = false;
        }
    }

    private async Task RefreshNow()
    {
        if (this.SelectedReportTarget is not { } target)
        {
            return;
        }

        await this.RefreshTargetAsync(target).ConfigureAwait(false);
    }

    private async Task RefreshTargetAsync(GenerationReportTarget target)
    {
        _refreshing = true;
        try
        {
            await Client.RefreshAsync(target, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task SelectIndexAsync(int index)
    {
        _activePanelIndex = index;
        GenerationReportTarget? target = DashboardReportTargets.Resolve(index);
        StateActions.SetActiveReportTarget(target);

        if (target is not null && State.HasApiKey)
        {
            await this.RefreshTargetAsync(target.Value).ConfigureAwait(false);
        }
    }

    private Task SelectReportAsync(GenerationReportTarget target)
    {
        StateActions.SetReportNavigationOpen(true);
        return this.SelectIndexAsync(DashboardReportTargets.IndexOf(target));
    }

    private Task SelectLogsAsync()
    {
        StateActions.SetReportNavigationOpen(true);
        StateActions.SetConfigView(false);
        return this.SelectIndexAsync(DashboardReportTargets.Count);
    }

    private void SelectConfig()
    {
        StateActions.SetReportNavigationOpen(true);
        StateActions.SetConfigView(true);
    }

    private void TogglePolling() => StateActions.SetPaused(!State.IsPollingPaused);

    private void ClearLogs() => StateActions.ClearLogs();

    private void OnPollIntervalChanged(int value) => StateActions.SetPollIntervalMs(value);

    private void OnPingIntervalChanged(int value) => StateActions.SetPingIntervalMs(value);

    private void OnRequestTimeoutChanged(int value) => StateActions.SetRequestTimeoutMs(value);

    private void OnBackendAddressChanged(string value) => StateActions.SetBackendAddress(value);

    private void OnBackendPortChanged(int value) => StateActions.SetBackendPort(value);

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}
