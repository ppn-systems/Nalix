using Nalix.Examples.Contracts.Packets;
using Nalix.Examples.Dashboard.Domain.Reports;

namespace Nalix.Examples.Dashboard.Application.State;

internal interface IDashboardStateWriter
{
    void SetEndpoint(string endpoint);

    void SetPaused(bool paused);

    void SetReportNavigationOpen(bool open);

    void SetActiveReportTarget(GenerationReportTarget? target);

    void SetApiKeyConfigured(bool configured);

    void Log(string level, string message);

    void ClearLogs();

    void MarkConnected();

    void MarkDisconnected(string? error);

    void UpdatePing(double milliseconds);

    void UpdateReport(DashboardReportSnapshot report);
}
