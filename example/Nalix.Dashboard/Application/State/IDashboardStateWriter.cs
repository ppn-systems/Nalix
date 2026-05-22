using Nalix.Dashboard.Domain.Reports;

namespace Nalix.Dashboard.Application.State;

internal interface IDashboardStateWriter
{
    void SetApiKeyConfigured(bool configured);

    void Log(string level, string message);

    void ClearLogs();

    void MarkConnected();

    void MarkDisconnected(string? error);

    void UpdatePing(double milliseconds);

    void UpdateReport(DashboardReportSnapshot report);
}
