using Nalix.Examples.Contracts.Packets;
using Nalix.Examples.Dashboard.Application.Reports;
using Nalix.Examples.Dashboard.Domain.Reports;

namespace Nalix.Examples.Dashboard.Components.Pages;

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
        return this.SelectIndexAsync(DashboardReportTargets.Count);
    }

    private void TogglePolling() => StateActions.SetPaused(!State.IsPollingPaused);

    private void ClearLogs() => StateActions.ClearLogs();

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}
