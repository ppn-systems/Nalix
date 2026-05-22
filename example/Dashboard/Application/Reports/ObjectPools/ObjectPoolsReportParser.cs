using System.Text.Json;
using Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.ObjectPools;

namespace Nalix.Dashboard.Application.Reports.ObjectPools;

internal sealed class ObjectPoolsReportParser : IReportParser<ObjectPoolsReport>
{
    public GenerationReportTarget Target => GenerationReportTarget.OBJECT_POOLS;

    public bool CanParse(GenerationReportTarget target) => target == GenerationReportTarget.OBJECT_POOLS;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public ObjectPoolsReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ObjectPoolsReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
