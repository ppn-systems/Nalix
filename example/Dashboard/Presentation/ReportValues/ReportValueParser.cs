using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dashboard.Presentation.ReportValues;

public enum ParsedReportValueKind
{
    Empty,
    Scalar,
    Fields,
    Records,
    Items
}

public sealed record ParsedReportField(string Name, string Value);

public sealed record ParsedReportRecord(string Title, IReadOnlyList<ParsedReportField> Fields);

public sealed record ParsedReportValue(
    ParsedReportValueKind Kind,
    string Raw,
    IReadOnlyList<ParsedReportField> Fields,
    IReadOnlyList<ParsedReportRecord> Records,
    IReadOnlyList<string> Items)
{
    public static ParsedReportValue Empty { get; } = new(
        ParsedReportValueKind.Empty,
        string.Empty,
        [],
        [],
        []);

    public bool IsWide =>
        this.Kind is ParsedReportValueKind.Records or ParsedReportValueKind.Items ||
        this.Fields.Count > 3 ||
        this.Raw.Length > 96;
}

public static partial class ReportValueParser
{
    private static readonly IReadOnlyDictionary<string, string[]> s_recordFieldOrders =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["PendingByConnection"] = ["EndPoint", "Pending"],
            ["Pools"] =
            [
                "BufferSize", "Initial", "Total", "Free", "InUse", "Hits", "Expands",
                "Shrinks", "UsageRatio", "MissRate", "ShrinkSkipped", "BytesReturned"
            ],
            ["Instances"] = ["Type", "IsDisposable", "Source"],
            ["Recurring"] =
            [
                "Name", "TotalRuns", "ConsecutiveFailures", "IsRunning", "LastRunUtc",
                "NextRunUtc", "IntervalMs", "Tag"
            ],
            ["SampleConnections"] = ["ID", "Username", "Level", "Algorithm", "BytesSent", "UpTime"],
            ["TopRecurringByFailures"] = ["Name", "ConsecutiveFailures", "LastRunUtc", "Tag"],
            ["TopRunningWorkers"] = ["Id", "Name", "Group", "StartedUtc", "Progress", "LastHeartbeatUtc"],
            ["TopEndpoints"] = ["Address", "CurrentConnections", "TotalConnectionsToday", "LastConnectionUtc"]
        };

    [GeneratedRegex(@"(?<name>[^:,]+):\s*Running:\s*(?<running>[^,]+),\s*Total:\s*(?<total>[^,]+),\s*Concurrency:\s*(?<concurrency>[^,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex WorkerGroupRegex();

    [GeneratedRegex(@"(?:^|,\s*)(?<key>[A-Za-z][A-Za-z0-9_./ -]{0,64}):\s*", RegexOptions.CultureInvariant)]
    private static partial Regex GenericFieldRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex KeyBreakRegex();

    public static ParsedReportValue Parse(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (value is null)
        {
            return ParsedReportValue.Empty;
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            return ParseDictionary(key, dictionary);
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            return ParseSequence(key, sequence);
        }

        string raw = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return ParseString(key, raw);
    }

    private static ParsedReportValue ParseString(string key, string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return ParsedReportValue.Empty;
        }

        if (key.Equals("WorkersByGroup", StringComparison.Ordinal) &&
            TryParseWorkerGroups(raw, out IReadOnlyList<ParsedReportRecord>? groupRecords))
        {
            return new ParsedReportValue(ParsedReportValueKind.Records, raw, [], groupRecords, []);
        }

        if (TryParseOrderedRecords(key, raw, out IReadOnlyList<ParsedReportRecord>? records))
        {
            return new ParsedReportValue(ParsedReportValueKind.Records, raw, [], records, []);
        }

        if (TryParseFields(raw, out IReadOnlyList<ParsedReportField>? fields))
        {
            return new ParsedReportValue(ParsedReportValueKind.Fields, raw, fields, [], []);
        }

        if (TrySplitItems(raw, out IReadOnlyList<string>? items))
        {
            return new ParsedReportValue(ParsedReportValueKind.Items, raw, [], [], items);
        }

        return new ParsedReportValue(ParsedReportValueKind.Scalar, raw, [], [], []);
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private static ParsedReportValue ParseDictionary(string key, System.Collections.IDictionary dictionary)
    {
        if (dictionary.Count == 0)
        {
            return ParsedReportValue.Empty;
        }

        List<ParsedReportField> fields = new(dictionary.Count);
        List<ParsedReportRecord> records = new(dictionary.Count);
        bool allNestedObjects = true;

        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            string name = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            if (entry.Value is System.Collections.IDictionary nested)
            {
                IReadOnlyList<ParsedReportField> nestedFields = ToFields(nested);
                records.Add(new ParsedReportRecord(name, nestedFields));
            }
            else
            {
                allNestedObjects = false;
            }

            fields.Add(new ParsedReportField(name, FormatValue(entry.Value)));
        }

        string raw = string.Join(", ", fields.Select(static field => $"{field.Name}: {field.Value}"));
        return allNestedObjects && records.Count > 0
            ? new ParsedReportValue(ParsedReportValueKind.Records, raw, [], records, [])
            : new ParsedReportValue(ParsedReportValueKind.Fields, raw, fields, [], []);
    }

    private static ParsedReportValue ParseSequence(string key, System.Collections.IEnumerable sequence)
    {
        List<string> items = [];
        List<ParsedReportRecord> records = [];
        bool allRecords = true;

        foreach (object? item in sequence)
        {
            if (item is System.Collections.IDictionary dictionary)
            {
                IReadOnlyList<ParsedReportField> fields = ToFields(dictionary);
                string title = ResolveRecordTitle(key, records.Count, fields);
                records.Add(new ParsedReportRecord(title, fields));
                continue;
            }

            allRecords = false;
            items.Add(FormatValue(item));
        }

        if (allRecords && records.Count > 0)
        {
            string rawRecords = string.Join(", ", records.Select(static record => record.Title));
            return new ParsedReportValue(ParsedReportValueKind.Records, rawRecords, [], records, []);
        }

        string raw = string.Join(", ", items);
        return items.Count == 0
            ? ParsedReportValue.Empty
            : new ParsedReportValue(ParsedReportValueKind.Items, raw, [], [], items);
    }

    private static IReadOnlyList<ParsedReportField> ToFields(System.Collections.IDictionary dictionary)
    {
        List<ParsedReportField> fields = new(dictionary.Count);
        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            string name = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            fields.Add(new ParsedReportField(name, FormatValue(entry.Value)));
        }

        return fields;
    }

    public static string DisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string spaced = KeyBreakRegex().Replace(name.Replace('_', ' '), " ");

        return spaced.Trim();
    }

    private static bool TryParseWorkerGroups(
        string raw,
        [NotNullWhen(true)] out IReadOnlyList<ParsedReportRecord>? records)
    {
        MatchCollection matches = WorkerGroupRegex().Matches(raw);
        if (matches.Count == 0)
        {
            records = null;
            return false;
        }

        List<ParsedReportRecord> parsed = new(matches.Count);
        foreach (Match match in matches)
        {
            string title = match.Groups["name"].Value.Trim();
            parsed.Add(new ParsedReportRecord(
                title,
                [
                    new ParsedReportField("Running", match.Groups["running"].Value.Trim()),
                    new ParsedReportField("Total", match.Groups["total"].Value.Trim()),
                    new ParsedReportField("Concurrency", match.Groups["concurrency"].Value.Trim())
                ]));
        }

        records = parsed;
        return true;
    }

    private static bool TryParseOrderedRecords(
        string key,
        string raw,
        [NotNullWhen(true)] out IReadOnlyList<ParsedReportRecord>? records)
    {
        records = null;
        if (!s_recordFieldOrders.TryGetValue(key, out string[]? fieldOrder))
        {
            return false;
        }

        List<ParsedReportField> stream = ParseKnownFieldStream(raw, fieldOrder);
        if (stream.Count == 0)
        {
            return false;
        }

        List<ParsedReportRecord> parsed = [];
        List<ParsedReportField> current = [];
        string firstField = fieldOrder[0];

        foreach (ParsedReportField field in stream)
        {
            if (field.Name.Equals(firstField, StringComparison.Ordinal) && current.Count > 0)
            {
                AddRecord(key, parsed.Count, current, parsed);
                current = [];
            }

            current.Add(field);
        }

        if (current.Count > 0)
        {
            AddRecord(key, parsed.Count, current, parsed);
        }

        records = parsed;
        return parsed.Count > 0;
    }

    private static List<ParsedReportField> ParseKnownFieldStream(string raw, IReadOnlyList<string> fieldOrder)
    {
        string pattern = @"(?:^|,\s*)(?<key>" + string.Join('|', fieldOrder.Select(Regex.Escape)) + @"):\s*";
        MatchCollection matches = Regex.Matches(raw, pattern, RegexOptions.CultureInvariant);
        return BuildFieldsFromMatches(raw, matches);
    }

    private static bool TryParseFields(
        string raw,
        [NotNullWhen(true)] out IReadOnlyList<ParsedReportField>? fields)
    {
        List<ParsedReportField> parsed = BuildFieldsFromMatches(raw, GenericFieldRegex().Matches(raw));
        if (parsed.Count == 0)
        {
            fields = null;
            return false;
        }

        fields = parsed;
        return true;
    }

    private static List<ParsedReportField> BuildFieldsFromMatches(string raw, MatchCollection matches)
    {
        List<ParsedReportField> parsed = new(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            int valueStart = match.Index + match.Length;
            int valueEnd = i + 1 < matches.Count ? matches[i + 1].Index : raw.Length;
            string value = raw[valueStart..valueEnd].Trim().TrimEnd(',');
            string fieldName = match.Groups["key"].Value.Trim();

            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                parsed.Add(new ParsedReportField(fieldName, value));
            }
        }

        return parsed;
    }

    private static void AddRecord(
        string key,
        int index,
        IReadOnlyList<ParsedReportField> fields,
        List<ParsedReportRecord> records)
    {
        string title = ResolveRecordTitle(key, index, fields);

        records.Add(new ParsedReportRecord(title, [.. fields]));
    }

    private static string ResolveRecordTitle(
        string key,
        int index,
        IReadOnlyList<ParsedReportField> fields)
    {
        ParsedReportField? titleField = fields.FirstOrDefault(static f =>
            f.Name is "Name" or "Type" or "ID" or "Id" or "EndPoint" or "Address" or "BufferSize" or "Username");
        string title = titleField?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"{DisplayName(key)} {index + 1}";
        }

        return title;
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "-";
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            return string.Join(", ", ToFields(dictionary).Select(static field => $"{field.Name}: {field.Value}"));
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            return string.Join(", ", sequence.Cast<object?>().Select(FormatValue));
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool TrySplitItems(
        string raw,
        [NotNullWhen(true)] out IReadOnlyList<string>? items)
    {
        if (raw.Length < 120 || !raw.Contains(", ", StringComparison.Ordinal))
        {
            items = null;
            return false;
        }

        string[] parts = [.. raw
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => !string.IsNullOrWhiteSpace(part))];

        if (parts.Length < 3)
        {
            items = null;
            return false;
        }

        items = parts;
        return true;
    }
}

