using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Buffers;

namespace Nalix.Dashboard.Application.Reports.Buffers;

internal sealed class BuffersReportParser : IReportParser<BuffersReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.BUFFERS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.BUFFERS;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public BuffersReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BuffersReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
