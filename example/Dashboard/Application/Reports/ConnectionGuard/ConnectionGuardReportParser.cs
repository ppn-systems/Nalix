using System.Text.Json;
using Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ConnectionGuard;

namespace Nalix.Dashboard.Application.Reports.ConnectionGuard;

internal sealed class ConnectionGuardReportParser : IReportParser<ConnectionGuardReport>
{
    public GenerationReportTarget Target => GenerationReportTarget.CONNECTION_GUARD;

    public bool CanParse(GenerationReportTarget target) => target == GenerationReportTarget.CONNECTION_GUARD;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public ConnectionGuardReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConnectionGuardReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
