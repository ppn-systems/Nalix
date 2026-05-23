using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.TokenBucketLimiter;

namespace Nalix.Dashboard.Application.Reports.TokenBucketLimiter;

internal sealed class TokenBucketLimiterReportParser : IReportParser<TokenBucketLimiterReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.TOKEN_BUCKET_LIMITER;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.TOKEN_BUCKET_LIMITER;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public TokenBucketLimiterReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TokenBucketLimiterReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
