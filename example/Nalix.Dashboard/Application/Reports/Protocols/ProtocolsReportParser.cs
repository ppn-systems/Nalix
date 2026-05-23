using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Protocols;

namespace Nalix.Dashboard.Application.Reports.Protocols;

internal sealed class ProtocolsReportParser : IReportParser<ProtocolsReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.PROTOCOL;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.PROTOCOL;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public ProtocolsReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProtocolsReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
