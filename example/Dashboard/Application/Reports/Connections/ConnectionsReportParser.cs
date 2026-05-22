using System.Text.Json;
using Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Connections;

namespace Nalix.Dashboard.Application.Reports.Connections;

internal sealed class ConnectionsReportParser : IReportParser<ConnectionsReport>
{
    public GenerationReportTarget Target => GenerationReportTarget.CONNECTIONS;

    public bool CanParse(GenerationReportTarget target) => target == GenerationReportTarget.CONNECTIONS;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public ConnectionsReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConnectionsReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
