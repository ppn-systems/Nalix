using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ObjectPools;

namespace Nalix.Dashboard.Application.Reports.ObjectPools;

internal sealed class ObjectPoolsReportParser : IReportParser<ObjectPoolsReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.OBJECT_POOLS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.OBJECT_POOLS;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public ObjectPoolsReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ObjectPoolsReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
