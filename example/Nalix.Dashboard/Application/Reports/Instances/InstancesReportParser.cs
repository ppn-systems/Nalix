using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Instances;

namespace Nalix.Dashboard.Application.Reports.Instances;

internal sealed class InstancesReportParser : IReportParser<InstancesReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.INSTANCES;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.INSTANCES;

    public object? Parse(string ObservationData) => ParseTyped(ObservationData);

    public InstancesReport? ParseTyped(string ObservationData)
    {
        if (string.IsNullOrWhiteSpace(ObservationData))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstancesReport>(ObservationData, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
