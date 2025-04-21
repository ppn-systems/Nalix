using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Nalix.Shared.L10N;

/// <summary>
/// Represents a Portable Object (PO) file for translations.
/// </summary>
public partial class PoFile
{
    #region Fields

    private readonly Dictionary<string, string> _metadata = [];
    private readonly Dictionary<string, string> _translations = [];
    private readonly Dictionary<string, string[]> _pluralTranslations = [];

    private Func<int, int> _pluralRule = n => n == 1 ? 0 : 1; // Standard rule (English)

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes an empty PO file.
    /// </summary>
    public PoFile() { }

    /// <summary>
    /// Initializes a <see cref="PoFile"/> by loading the specified PO file.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
    public PoFile(string path) => LoadFromFile(path);

    #endregion

    #region Public API

    /// <summary>
    /// Loads a PO file from the specified path and parses its contents.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        using var reader = new StreamReader(path, Encoding.UTF8);
        Parse(reader);
    }

    /// <summary>
    /// Parses the PO file content from a <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="TextReader"/> containing PO file content.</param>
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
                {
                    msgstrPlural[^1] += extracted;
                }
                else if (!string.IsNullOrEmpty(msgstr))
                {
                    msgstr += extracted;
                }
                else
                {
                    msgid += extracted;
                }
            }

            if (!string.IsNullOrEmpty(msgid) && !string.IsNullOrEmpty(msgstr))
            {
                _translations[msgid] = msgstr;
            }
            else if (!string.IsNullOrEmpty(msgid) && msgstrPlural.Count > 0)
            {
                _pluralTranslations[msgid] = [.. msgstrPlural];
            }
        }

        // Extract metadata
        if (_translations.TryGetValue("", out var metadata))
        {
            this.ParseMetadata(metadata);
        }
    }

    /// <summary>
    /// Gets the translated string for a given Number.
    /// </summary>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if available, otherwise returns the original text.</returns>
    public string GetString(string id)
        => _translations.TryGetValue(id, out var value) ? value : id;

    /// <summary>
    /// Gets the pluralized translation for a given Number.
    /// </summary>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The Number to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
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
    /// Retrieves a translation with context.
    /// </summary>
    /// <param name="context">Context to distinguish similar translations.</param>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if found, otherwise returns the original text.</returns>
    public string GetParticularString(string context, string id)
    {
        string key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context
        return _translations.TryGetValue(key, out var value) ? value : id;
    }

    /// <summary>
    /// Retrieves a pluralized translation with context.
    /// </summary>
    /// <param name="context">Context to distinguish similar translations.</param>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The Number to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    public string GetParticularPluralString(string context, string id, string idPlural, int n)
    {
        string key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context

        if (_pluralTranslations.TryGetValue(key, out var plurals))
        {
            int index = _pluralRule(n);
            if (index >= 0 && index < plurals.Length)
                return plurals[index];
        }

        return n == 1 ? id : idPlural;
    }

    /// <summary>
    /// Retrieves metadata value from the PO file.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The metadata value if found, otherwise <c>null</c>.</returns>
    public string? GetMetadata(string key)
        => _metadata.TryGetValue(key, out var value) ? value : null;

    #endregion

    #region Private API

    /// <summary>
    /// Extracts quoted value from a line.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ExtractQuotedValue(string line)
    {
        Match match = ExtractQuotedText().Match(line);
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Extracts plural index from msgstr[N].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtractPluralIndex(string line)
    {
        Match match = ExtractPluralIndex().Match(line);
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
        foreach (string line in metadata.Split("\\n"))
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length == 2)
                _metadata[parts[0].Trim()] = parts[1].Trim();
        }

        // Set plural rule if available
        if (_metadata.TryGetValue("Plural-Forms", out string? pluralForms))
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
            { "nplurals=1; plural=0;", n => 0 },                     // Japanese, Chinese, Vietnam
            { "nplurals=2; plural=(n != 1);", n => n == 1 ? 0 : 1 }, // English, Spanish, Italian
            { "nplurals=2; plural=(n > 1);", n => n > 1 ? 1 : 0 }    // French, Portuguese
        };

        foreach (var kvp in rules)
            if (rule.Contains(kvp.Key)) return kvp.Value;

        return n => n == 1 ? 0 : 1;
    }

    [GeneratedRegex(@"msgstr\[(\d+)\]")]
    private static partial Regex ExtractPluralIndex();

    [GeneratedRegex("\"(.*?)\"")]
    private static partial Regex ExtractQuotedText();

    #endregion
}
