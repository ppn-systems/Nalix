using System.Globalization;
using Nalix.Framework.Extensions;

namespace Dashboard.Presentation;

internal static class NumberDisplayFormatter
{
    private const string CompactDecimalFormat = "0.###";
    private const NumberStyles NumericStyles =
        NumberStyles.Float | NumberStyles.AllowThousands;

    public static string Format(double value)
    {
        double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded == 0
            ? "0"
            : rounded.ToString(CompactDecimalFormat, CultureInfo.InvariantCulture);
    }

    public static string Format(decimal value)
    {
        decimal rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded == 0
            ? "0"
            : rounded.ToString(CompactDecimalFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatCompact(long value) => value.FormatCompact();

    public static string FormatCompact(int value) => ((long)value).FormatCompact();

    public static bool TryFormat(string value, out string formatted)
    {
        string trimmed = value.Trim();

        if (decimal.TryParse(trimmed, NumericStyles, CultureInfo.InvariantCulture, out decimal decimalValue))
        {
            formatted = Format(decimalValue);
            return true;
        }

        if (double.TryParse(trimmed, NumericStyles, CultureInfo.InvariantCulture, out double doubleValue) &&
            double.IsFinite(doubleValue))
        {
            formatted = Format(doubleValue);
            return true;
        }

        formatted = string.Empty;
        return false;
    }
}
