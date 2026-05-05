namespace Nalix.Examples.Dashboard.Presentation.ReportValues;

public sealed record ReportRecordFieldItem(
    string Name,
    string Label,
    string RawValue,
    string Value,
    bool IsWide);

public sealed record ReportRecordItem(
    string Title,
    IReadOnlyList<ReportRecordFieldItem> Fields,
    bool IsCompact);

public sealed record ReportRecordLayout(
    IReadOnlyList<ReportRecordItem> Records,
    bool HasCompactRecords);

public static class ReportRecordLayoutBuilder
{
    public static ReportRecordLayout Build(IReadOnlyList<ParsedReportRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        List<ReportRecordItem> items = new(records.Count);
        foreach (ParsedReportRecord record in records)
        {
            items.Add(CreateItem(record));
        }

        int compactCount = items.Count(static item => item.IsCompact);
        return new ReportRecordLayout(items, compactCount >= 2);
    }

    private static ReportRecordItem CreateItem(ParsedReportRecord record)
    {
        string title = FormatRecordTitle(record);
        List<ReportRecordFieldItem> fields = [];

        foreach (ParsedReportField field in record.Fields)
        {
            if (ShouldRenderField(record, title, field))
            {
                fields.Add(CreateField(field));
            }
        }

        return new ReportRecordItem(title, fields, IsCompactRecord(title, fields));
    }

    private static ReportRecordFieldItem CreateField(ParsedReportField field)
    {
        string value = FormatFieldValue(field);
        return new ReportRecordFieldItem(
            field.Name,
            ReportValueParser.DisplayName(field.Name),
            field.Value,
            value,
            IsLongValue(value) || IsDateField(field.Name, field.Value));
    }

    private static bool ShouldRenderField(ParsedReportRecord record, string title, ParsedReportField field)
    {
        if (field.Name is "Name" or "Id" or "ID")
        {
            return !record.Title.Equals(field.Value, StringComparison.OrdinalIgnoreCase);
        }

        return !IsTypeField(field) ||
               !title.Equals(FormatFieldValue(field), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompactRecord(string title, IReadOnlyList<ReportRecordFieldItem> fields)
    {
        if (fields.Count is 0 or > 3 || title.Length > 44)
        {
            return false;
        }

        return fields.All(IsCompactField);
    }

    private static bool IsCompactField(ReportRecordFieldItem field)
        => !field.IsWide &&
           field.Label.Length <= 24 &&
           field.Value.Length <= 28 &&
           !field.Value.Contains('\n', StringComparison.Ordinal) &&
           !field.Value.Contains('\r', StringComparison.Ordinal) &&
           !ReportValueFormatter.IsCodeValue(field.RawValue);

    private static string FormatFieldValue(ParsedReportField field)
        => IsTypeField(field)
            ? ReportValueFormatter.FormatTypeName(field.Value)
            : ReportValueFormatter.Format(field.Value);

    private static string FormatRecordTitle(ParsedReportRecord record)
        => record.Fields.Any(IsTypeField)
            ? ReportValueFormatter.FormatTypeName(record.Title)
            : ReportValueFormatter.Format(record.Title);

    private static bool IsTypeField(ParsedReportField field)
        => field.Name.Equals("Type", StringComparison.OrdinalIgnoreCase);

    private static bool IsDateField(string name, string value)
        => name.Contains("Utc", StringComparison.OrdinalIgnoreCase) ||
           ReportValueFormatter.IsDateValue(value);

    private static bool IsLongValue(string value)
        => value.Length > 28;
}
