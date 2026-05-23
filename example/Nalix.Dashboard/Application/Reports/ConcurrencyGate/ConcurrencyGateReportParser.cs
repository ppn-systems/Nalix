using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ConcurrencyGate;

namespace Nalix.Dashboard.Application.Reports.ConcurrencyGate;

internal sealed class ConcurrencyGateReportParser : IReportParser<ConcurrencyGateReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.CONCURRENCY_GATE;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.CONCURRENCY_GATE;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public ConcurrencyGateReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConcurrencyGateReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
