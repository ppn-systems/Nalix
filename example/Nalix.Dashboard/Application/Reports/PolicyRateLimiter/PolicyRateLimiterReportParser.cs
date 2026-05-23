using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.PolicyRateLimiter;

namespace Nalix.Dashboard.Application.Reports.PolicyRateLimiter;

internal sealed class PolicyRateLimiterReportParser : IReportParser<PolicyRateLimiterReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.POLICY_RATE_LIMITER;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.POLICY_RATE_LIMITER;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public PolicyRateLimiterReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PolicyRateLimiterReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
