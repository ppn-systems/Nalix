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

    public static string FormatTypeName(string value)
    {
        string formatted = Format(value);
        return FormatClrTypeName(formatted);
    }

    private static string FormatClrTypeName(string value)
    {
        string trimmed = StripTopLevelAssemblyMetadata(value.Trim());
        int arityMarker = trimmed.IndexOf('`', StringComparison.Ordinal);
        int genericStart = trimmed.IndexOf('[', StringComparison.Ordinal);

        if (arityMarker < 0 || genericStart < 0 || genericStart < arityMarker)
        {
            return FormatSimpleTypeName(arityMarker >= 0 ? trimmed[..arityMarker] : trimmed);
        }

        string baseName = FormatSimpleTypeName(trimmed[..arityMarker]);
        IReadOnlyList<string> arguments = ReadGenericArguments(trimmed, genericStart);

        return arguments.Count == 0
            ? baseName
            : string.Create(CultureInfo.InvariantCulture, $"{baseName}<{string.Join(", ", arguments)}>");
    }

    private static string StripTopLevelAssemblyMetadata(string value)
    {
        int bracketDepth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (current == '[')
            {
                bracketDepth++;
            }
            else if (current == ']')
            {
                bracketDepth = Math.Max(0, bracketDepth - 1);
            }
            else if (current == ',' && bracketDepth == 0)
            {
                return value[..i].Trim();
            }
        }

        return value;
    }

    private static string FormatSimpleTypeName(string value)
    {
        string trimmed = value.Trim();
        int namespaceSeparator = trimmed.LastIndexOf('.');
        int nestedSeparator = trimmed.LastIndexOf('+');
        int separator = Math.Max(namespaceSeparator, nestedSeparator);

        return separator >= 0 && separator + 1 < trimmed.Length
            ? trimmed[(separator + 1)..]
            : trimmed;
    }

    private static IReadOnlyList<string> ReadGenericArguments(string value, int genericStart)
    {
        int genericEnd = FindMatchingBracket(value, genericStart);
        if (genericEnd < 0)
        {
            return [];
        }

        List<string> arguments = [];
        int index = genericStart + 1;
        while (index < genericEnd)
        {
            while (index < genericEnd && (value[index] == ',' || char.IsWhiteSpace(value[index])))
            {
                index++;
            }

            if (index >= genericEnd)
            {
                break;
            }

            string argument;
            if (value[index] == '[')
            {
                int argumentEnd = FindMatchingBracket(value, index);
                if (argumentEnd < 0 || argumentEnd > genericEnd)
                {
                    break;
                }

                argument = value[(index + 1)..argumentEnd];
                index = argumentEnd + 1;
            }
            else
            {
                int argumentStart = index;
                while (index < genericEnd && value[index] != ',')
                {
                    index++;
                }

                argument = value[argumentStart..index];
            }

            if (!string.IsNullOrWhiteSpace(argument))
            {
                arguments.Add(FormatClrTypeName(argument));
            }
        }

        return arguments;
    }

    private static int FindMatchingBracket(string value, int start)
    {
        int depth = 0;
        for (int i = start; i < value.Length; i++)
        {
            if (value[i] == '[')
            {
                depth++;
            }
            else if (value[i] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
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
