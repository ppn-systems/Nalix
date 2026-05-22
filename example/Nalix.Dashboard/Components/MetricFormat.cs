using System.Globalization;
using System.Text;

namespace Nalix.Dashboard.Components;

public static class MetricFormat
{
    public static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Count(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Number(double value, int digits = 1)
        => value.ToString($"N{digits}", CultureInfo.InvariantCulture);

    public static string PercentRatio(double value, int digits = 1)
        => $"{(value * 100).ToString($"N{digits}", CultureInfo.InvariantCulture)}%";

    public static string Percent(double value, int digits = 1)
        => $"{value.ToString($"N{digits}", CultureInfo.InvariantCulture)}%";

    public static string Milliseconds(double value)
        => $"{value.ToString("N1", CultureInfo.InvariantCulture)} ms";

    public static string Seconds(double value)
        => $"{value.ToString("N0", CultureInfo.InvariantCulture)} s";

    public static string Bytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:N2} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:N2} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:N1} KB";
        return $"{bytes:N0} B";
    }

    public static string DateTimeOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-", StringComparison.Ordinal)
            ? "-"
            : value;

    public static string TypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        string trimmed = value.Trim();
        int genericTick = trimmed.IndexOf('`', StringComparison.Ordinal);
        int argsStart = trimmed.IndexOf("[[", StringComparison.Ordinal);
        if (genericTick > 0 && argsStart > genericTick)
        {
            string baseName = ShortName(trimmed[..genericTick]);
            List<string> args = ParseClrGenericArguments(trimmed[argsStart..]);
            return args.Count == 0 ? baseName : $"{baseName}<{string.Join(", ", args)}>";
        }

        int assemblySeparator = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (assemblySeparator > 0)
        {
            trimmed = trimmed[..assemblySeparator];
        }

        return ShortName(trimmed);
    }

    private static string ShortName(string value)
    {
        string trimmed = value.Trim().Replace('+', '.');
        int dot = trimmed.LastIndexOf('.');
        string name = dot >= 0 && dot + 1 < trimmed.Length ? trimmed[(dot + 1)..] : trimmed;
        return name switch
        {
            "Boolean" => "bool",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "Char" => "char",
            "Decimal" => "decimal",
            "Double" => "double",
            "Single" => "float",
            "Int32" => "int",
            "UInt32" => "uint",
            "Int64" => "long",
            "UInt64" => "ulong",
            "Int16" => "short",
            "UInt16" => "ushort",
            "Object" => "object",
            "String" => "string",
            _ => name
        };
    }

    private static List<string> ParseClrGenericArguments(string value)
    {
        List<string> args = [];
        if (!value.StartsWith("[[", StringComparison.Ordinal) ||
            !value.EndsWith("]]", StringComparison.Ordinal))
        {
            return args;
        }

        string inner = value[2..^2];
        int start = 0;
        int depth = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            if (i + 1 < inner.Length && inner[i] == '[' && inner[i + 1] == '[')
            {
                depth++;
                i++;
                continue;
            }

            if (i + 1 < inner.Length && inner[i] == ']' && inner[i + 1] == ']')
            {
                depth = Math.Max(0, depth - 1);
                i++;
                continue;
            }

            if (depth == 0 &&
                i + 2 < inner.Length &&
                inner[i] == ']' &&
                inner[i + 1] == ',' &&
                inner[i + 2] == '[')
            {
                args.Add(TypeName(StripAssembly(inner[start..i])));
                start = i + 3;
                i += 2;
            }
        }

        if (start < inner.Length)
        {
            args.Add(TypeName(StripAssembly(inner[start..])));
        }

        return args;
    }

    private static string StripAssembly(string value)
    {
        int genericStart = value.IndexOf("[[", StringComparison.Ordinal);
        int comma = value.IndexOf(',', StringComparison.Ordinal);
        if (comma > 0 && (genericStart < 0 || comma < genericStart))
        {
            return value[..comma];
        }

        if (genericStart > 0)
        {
            StringBuilder builder = new(value.Length);
            builder.Append(value[..genericStart]);
            builder.Append(value[genericStart..]);
            return builder.ToString();
        }

        return value;
    }
}
