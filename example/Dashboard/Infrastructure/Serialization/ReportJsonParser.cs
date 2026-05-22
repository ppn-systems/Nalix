using System.Text.Json;

namespace Nalix.Dashboard.Infrastructure.Serialization;

internal static class ReportJsonParser
{
    private static readonly JsonSerializerOptions s_options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyDictionary<string, object?> Parse(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(dataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CreateRawFallback(dataJson);
            }

            Dictionary<string, object?> data = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                data[property.Name] = ConvertElement(property.Value);
            }

            return data;
        }
        catch (JsonException)
        {
            return CreateRawFallback(dataJson);
        }
    }

    private static Dictionary<string, object?> CreateRawFallback(string dataJson)
        => new(StringComparer.Ordinal) { ["Data"] = dataJson };

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    static p => p.Name,
                    static p => ConvertElement(p.Value),
                    StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.True => (object?)true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static object ConvertNumber(JsonElement element)
    {
        if (element.TryGetInt64(out long l))
        {
            return l;
        }

        return element.TryGetDecimal(out decimal d) ? d : element.GetDouble();
    }
}
