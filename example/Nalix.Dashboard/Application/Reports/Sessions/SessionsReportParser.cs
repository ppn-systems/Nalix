using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Sessions;

namespace Nalix.Dashboard.Application.Reports.Sessions;

internal sealed class SessionsReportParser : IReportParser<SessionsReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.SESSIONS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.SESSIONS;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public SessionsReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SessionsReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
