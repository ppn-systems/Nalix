using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ConnectionGuard;

namespace Nalix.Dashboard.Application.Reports.ConnectionGuard;

internal sealed class ConnectionGuardReportParser : IReportParser<ConnectionGuardReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.CONNECTION_GUARD;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.CONNECTION_GUARD;

    public object? Parse(string ObservationData) => ParseTyped(ObservationData);

    public ConnectionGuardReport? ParseTyped(string ObservationData)
    {
        if (string.IsNullOrWhiteSpace(ObservationData))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConnectionGuardReport>(ObservationData, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
