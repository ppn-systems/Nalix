using System.Text.Json;
using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Tasks;

namespace Nalix.Dashboard.Application.Reports.Tasks;

internal sealed class TasksReportParser : IReportParser<TasksReport>
{
    public RuntimeObservationTarget Target => RuntimeObservationTarget.TASKS;

    public bool CanParse(RuntimeObservationTarget target) => target == RuntimeObservationTarget.TASKS;

    public object? Parse(ReadOnlyMemory<byte> ObservationData) => ParseTyped(ObservationData);

    public TasksReport? ParseTyped(ReadOnlyMemory<byte> ObservationData)
    {
        if (ObservationData.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TasksReport>(ObservationData.Span, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
