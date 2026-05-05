using System.Globalization;
using Nalix.Examples.Dashboard.Presentation;

namespace Nalix.Examples.Dashboard.Presentation.ReportValues;

internal static class ReportValueFormatter
{
    public static string Format(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        string trimmed = value.Trim();
        if (TryFormatDate(trimmed, out string dateValue))
        {
            return dateValue;
        }

        if (IsCodeValue(trimmed))
        {
            return trimmed;
        }

        return NumberDisplayFormatter.TryFormat(trimmed, out string numericValue)
            ? numericValue
            : trimmed;
    }

    public static bool IsDateValue(string value)
        => value.Contains('T', StringComparison.Ordinal) &&
           DateTimeOffset.TryParse(
               value,
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal,
               out _);

    public static bool IsBooleanValue(string value)
        => bool.TryParse(value, out _);

    public static bool IsCodeValue(string value)
        => value.Length >= 12 && value.All(static c => Uri.IsHexDigit(c));

    private static bool TryFormatDate(string value, out string formatted)
    {
        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed))
        {
            formatted = parsed.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'",
                CultureInfo.InvariantCulture);
            return true;
        }

        formatted = string.Empty;
        return false;
    }
}
