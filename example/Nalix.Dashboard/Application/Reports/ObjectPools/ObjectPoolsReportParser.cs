using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ObjectPools;

namespace Nalix.Dashboard.Application.Reports.ObjectPools;

internal sealed class ObjectPoolsReportParser : IReportParser<ObjectPoolsReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.OBJECT_POOLS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.OBJECT_POOLS;

    public object? Parse(string ObservationData) => ParseTyped(ObservationData);

    public ObjectPoolsReport? ParseTyped(string ObservationData)
    {
        if (string.IsNullOrWhiteSpace(ObservationData))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ObjectPoolsReport>(ObservationData, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
