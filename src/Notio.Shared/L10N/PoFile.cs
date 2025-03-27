using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Notio.Shared.L10N;

/// <summary>
/// Represents a Portable Object (PO) file for translations.
/// </summary>
public partial class PoFile
{
    private readonly Dictionary<string, string> _metadata = [];
    private readonly Dictionary<string, string> _translations = [];
    private readonly Dictionary<string, string[]> _pluralTranslations = [];

    private Func<int, int> _pluralRule = n => n == 1 ? 0 : 1; // Default rule (English)

    /// <summary>
    /// Initializes an empty PO file.
    /// </summary>
    public PoFile() { }

    /// <summary>
    /// Loads a PO file from the specified path.
    /// </summary>
    /// <param name="path">Path to the PO file.</param>
    public PoFile(string path) => LoadFromFile(path);

    /// <summary>
    /// Loads a PO file from the specified path.
    /// </summary>
    /// <param name="path">Path to the PO file.</param>
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        using var reader = new StreamReader(path, Encoding.UTF8);
        Parse(reader);
    }

    /// <summary>
    /// Parses the PO file content from a text reader.
    /// </summary>
    /// <param name="reader">Text reader containing PO file content.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0059:Unnecessary assignment of a value", Justification = "<Pending>")]
    private void Parse(TextReader reader)
    {
        string? line;
        string msgid = "", msgidPlural = "", msgstr = "";
        List<string> msgstrPlural = [];
        bool isPlural = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue; // Skip comments

            if (trimmed.StartsWith("msgid "))
            {
                msgid = ExtractQuotedValue(trimmed);
                isPlural = false;
                msgstr = "";
                msgstrPlural.Clear();
            }
            else if (trimmed.StartsWith("msgid_plural "))
            {
                msgidPlural = ExtractQuotedValue(trimmed);
                isPlural = true;
            }
            else if (trimmed.StartsWith("msgstr["))
            {
                int index = ExtractPluralIndex(trimmed);
                EnsurePluralListSize(msgstrPlural, index);
                msgstrPlural[index] = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith("msgstr "))
            {
                msgstr = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith('\"')) // Multiline string continuation
            {
                string extracted = ExtractQuotedValue(trimmed);
                if (isPlural && msgstrPlural.Count > 0)
                    msgstrPlural[^1] += extracted;
                else if (!string.IsNullOrEmpty(msgstr))
                    msgstr += extracted;
                else
                    msgid += extracted;
            }

            if (!string.IsNullOrEmpty(msgid) && !string.IsNullOrEmpty(msgstr))
                _translations[msgid] = msgstr;
            else if (!string.IsNullOrEmpty(msgid) && msgstrPlural.Count > 0)
                _pluralTranslations[msgid] = [.. msgstrPlural];
        }

        // Extract metadata
        if (_translations.TryGetValue("", out var metadata))
            ParseMetadata(metadata);
    }

    /// <summary>
    /// Gets a translated string.
    /// </summary>
    public string GetString(string id) => _translations.TryGetValue(id, out var value) ? value : id;

    /// <summary>
    /// Gets a translated string for plural forms.
    /// </summary>
    public string GetPluralString(string id, string idPlural, int n)
    {
        if (_pluralTranslations.TryGetValue(id, out var plurals))
        {
            int index = _pluralRule(n);
            if (index >= 0 && index < plurals.Length)
                return plurals[index];
        }
        return n == 1 ? id : idPlural;
    }

    /// <summary>
    /// Gets a translated string with context.
    /// </summary>
    public string GetParticularString(string context, string id)
    {
        string key = $"{context}\u0004{id}"; // PO sử dụng ký tự \u0004 để phân biệt ngữ cảnh
        return _translations.TryGetValue(key, out var value) ? value : id;
    }

    /// <summary>
    /// Gets a translated plural string with context.
    /// </summary>
    public string GetParticularPluralString(string context, string id, string idPlural, int n)
    {
        string key = $"{context}\u0004{id}"; // PO sử dụng ký tự \u0004 để phân biệt ngữ cảnh

        if (_pluralTranslations.TryGetValue(key, out var plurals))
        {
            int index = _pluralRule(n);
            if (index >= 0 && index < plurals.Length)
                return plurals[index];
        }

        return n == 1 ? id : idPlural;
    }

    /// <summary>
    /// Gets metadata value from the PO file.
    /// </summary>
    public string? GetMetadata(string key)
        => _metadata.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Extracts quoted value from a line.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ExtractQuotedValue(string line)
    {
        var match = ExtractQuotedText().Match(line);
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Extracts plural index from msgstr[N].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtractPluralIndex(string line)
    {
        var match = ExtractPluralIndex().Match(line);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Ensures the plural list has enough size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsurePluralListSize(List<string> list, int index)
    {
        while (list.Count <= index) list.Add("");
    }

    /// <summary>
    /// Parses metadata from the PO file.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ParseMetadata(string metadata)
    {
        foreach (var line in metadata.Split("\\n"))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
                _metadata[parts[0].Trim()] = parts[1].Trim();
        }

        // Set plural rule if available
        if (_metadata.TryGetValue("Plural-Forms", out var pluralForms))
            _pluralRule = ParsePluralRule(pluralForms);
    }

    /// <summary>
    /// Parses the plural rule from PO file metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<int, int> ParsePluralRule(string rule)
    {
        Dictionary<string, Func<int, int>> rules = new()
        {
        { "nplurals=1; plural=0;", n => 0 }, // Nhật, Trung, Thổ Nhĩ Kỳ (không có số nhiều)
        { "nplurals=2; plural=(n != 1);", n => n == 1 ? 0 : 1 }, // Anh, Tây Ban Nha, Ý
        { "nplurals=2; plural=(n > 1);", n => n > 1 ? 1 : 0 }, // Pháp, Bồ Đào Nha
        { "nplurals=3; plural=(n == 0 ? 0 : n == 1 ? 1 : 2);", n => n == 0 ? 0 : n == 1 ? 1 : 2 }, // Romania
        { "nplurals=3; plural=(n%10==1 && n%100!=11 ? 0 : (n%10>=2 && n%10<=4 && (n%100<10 || n%100>=20) ? 1 : 2));",
            n => (n % 10 == 1 && n % 100 != 11) ? 0 : ((n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20)) ? 1 : 2) }, // Nga, Ukraina, Serbia
        { "nplurals=4; plural=(n==1 ? 0 : n==2 ? 1 : (n>=3 && n<=10) ? 2 : 3);",
            n => n == 1 ? 0 : n == 2 ? 1 : (n >= 3 && n <= 10) ? 2 : 3 }, // Ả Rập
        { "nplurals=4; plural=(n==1 ? 0 : n==2 ? 1 : n>=3 ? 2 : 3);",
            n => n == 1 ? 0 : n == 2 ? 1 : n >= 3 ? 2 : 3 } // Slovenia
        };

        // Tìm và áp dụng quy tắc phù hợp
        foreach (var kvp in rules)
            if (rule.Contains(kvp.Key)) return kvp.Value;

        return n => n == 1 ? 0 : 1; // Mặc định giống tiếng Anh
    }

    [GeneratedRegex(@"msgstr\[(\d+)\]")]
    private static partial Regex ExtractPluralIndex();

    [GeneratedRegex("\"(.*?)\"")]
    private static partial Regex ExtractQuotedText();
}
