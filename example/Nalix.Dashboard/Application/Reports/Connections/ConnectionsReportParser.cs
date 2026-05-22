using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Connections;

namespace Nalix.Dashboard.Application.Reports.Connections;

internal sealed class ConnectionsReportParser : IReportParser<ConnectionsReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.CONNECTIONS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.CONNECTIONS;

    public object? Parse(string ObservationData) => ParseTyped(ObservationData);

    public ConnectionsReport? ParseTyped(string ObservationData)
    {
        if (string.IsNullOrWhiteSpace(ObservationData))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConnectionsReport>(ObservationData, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
