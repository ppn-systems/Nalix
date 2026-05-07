using System.Text.Json;

namespace Dashboard.Application.Reports;

internal static class GenerationReportDataParser
{
    public static IReadOnlyDictionary<string, object?> Parse(string dataJson)
        => Parse(dataJson, out _);

    public static IReadOnlyDictionary<string, object?> Parse(string dataJson, out bool usedRawFallback)
    {
        usedRawFallback = false;

        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(dataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                usedRawFallback = true;
                return CreateRawData(dataJson);
            }

            Dictionary<string, object?> data = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                data[property.Name] = ConvertJsonElement(property.Value);
            }

            return data;
        }
        catch (JsonException)
        {
            usedRawFallback = true;
            return CreateRawData(dataJson);
        }
    }

    private static Dictionary<string, object?> CreateRawData(string dataJson)
        => new(StringComparer.Ordinal)
        {
            ["Data"] = dataJson
        };

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    static property => property.Name,
                    static property => ConvertJsonElement(property.Value),
                    StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertJsonNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static object ConvertJsonNumber(JsonElement element)
    {
        if (element.TryGetInt64(out long integer))
        {
            return integer;
        }

        return element.TryGetDecimal(out decimal decimalValue)
            ? decimalValue
            : element.GetDouble();
    }
}

