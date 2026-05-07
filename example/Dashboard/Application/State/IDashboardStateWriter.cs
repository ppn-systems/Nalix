using Contracts;
using Dashboard.Domain.Reports;

namespace Dashboard.Application.State;

internal interface IDashboardStateWriter
{
    void SetEndpoint(string endpoint);

    void SetBackendAddress(string address);

    void SetBackendPort(int port);

    void SetPaused(bool paused);

    void SetReportNavigationOpen(bool open);

    void SetConfigView(bool isConfig);

    void SetActiveReportTarget(GenerationReportTarget? target);

    void SetApiKeyConfigured(bool configured);

    void SetPollIntervalMs(int ms);

    void SetPingIntervalMs(int ms);

    void SetRequestTimeoutMs(int ms);

    void Log(string level, string message);

    void ClearLogs();

    void MarkConnected();

    void MarkDisconnected(string? error);

    void UpdatePing(double milliseconds);

    void UpdateReport(DashboardReportSnapshot report);
}
