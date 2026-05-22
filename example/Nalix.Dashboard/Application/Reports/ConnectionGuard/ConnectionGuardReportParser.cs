using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ConnectionGuard;

namespace Nalix.Dashboard.Application.Reports.ConnectionGuard;

internal sealed class ConnectionGuardReportParser : IReportParser<ConnectionGuardReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.CONNECTION_GUARD;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.CONNECTION_GUARD;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public ConnectionGuardReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConnectionGuardReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
