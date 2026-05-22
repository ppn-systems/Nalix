using System.Text.Json;
using Contracts;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Domain.Reports.Buffers;

namespace Nalix.Dashboard.Application.Reports.Buffers;

internal sealed class BuffersReportParser : IReportParser<BuffersReport>
{
    public GenerationReportTarget Target => GenerationReportTarget.BUFFERS;

    public bool CanParse(GenerationReportTarget target) => target == GenerationReportTarget.BUFFERS;

    public object? Parse(string dataJson) => ParseTyped(dataJson);

    public BuffersReport? ParseTyped(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BuffersReport>(dataJson, ReportJsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
