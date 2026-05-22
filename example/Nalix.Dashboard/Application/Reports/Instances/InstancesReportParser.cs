using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Instances;

namespace Nalix.Dashboard.Application.Reports.Instances;

internal sealed class InstancesReportParser : IReportParser<InstancesReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.INSTANCES;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.INSTANCES;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public InstancesReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstancesReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
