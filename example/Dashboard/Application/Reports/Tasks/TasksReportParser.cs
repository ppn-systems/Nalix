using System.Text.Json;
using Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Tasks;

namespace Nalix.Dashboard.Application.Reports.Tasks;

internal sealed class TasksReportParser : IReportParser<TasksReport>
{
    public GenerationReportTarget Target => GenerationReportTarget.TASKS;

    public bool CanParse(GenerationReportTarget target) => target == GenerationReportTarget.TASKS;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public TasksReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TasksReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
