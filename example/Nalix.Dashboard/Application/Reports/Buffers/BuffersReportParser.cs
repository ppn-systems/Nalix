using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Buffers;

namespace Nalix.Dashboard.Application.Reports.Buffers;

internal sealed class BuffersReportParser : IReportParser<BuffersReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.BUFFERS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.BUFFERS;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public BuffersReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BuffersReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
