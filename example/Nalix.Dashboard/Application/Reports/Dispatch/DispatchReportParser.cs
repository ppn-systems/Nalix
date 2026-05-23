using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Dispatch;

namespace Nalix.Dashboard.Application.Reports.Dispatch;

internal sealed class DispatchReportParser : IReportParser<DispatchReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.DISPATCH;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.DISPATCH;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public DispatchReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DispatchReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
