using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nalix.Dashboard.Application.Reports;

internal static class ReportJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
