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

    private readonly Dictionary<String, String> _metadata = [];
    private readonly Dictionary<String, String> _translations = [];
    private readonly Dictionary<String, String[]> _pluralTranslations = [];

    private Func<Int32, Int32> _pluralRule = n => n == 1 ? 0 : 1; // Standard rule (English)

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes an empty PO file.
    /// </summary>
    public PoFile()
    { }

    /// <summary>
    /// Initializes a <see cref="PoFile"/> by loading the specified PO file.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
    public PoFile(String path) => LoadFromFile(path);

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Loads a PO file from the specified path and parses its contents.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public void LoadFromFile(String path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

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
        String? line;
        String msgid = "", msgidPlural = "", msgstr = "";
        List<String> msgstrPlural = [];
        Boolean isPlural = false;

        while ((line = reader.ReadLine()) != null)
        {
            String trimmed = line.Trim();
            if (String.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue; // Skip comments
            }

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
                Int32 index = ExtractPluralIndex(trimmed);
                EnsurePluralListSize(msgstrPlural, index);
                msgstrPlural[index] = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith("msgstr "))
            {
                msgstr = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith('\"')) // Multiline string continuation
            {
                String extracted = ExtractQuotedValue(trimmed);

                if (isPlural && msgstrPlural.Count > 0)
                {
                    msgstrPlural[^1] += extracted;
                }
                else if (!String.IsNullOrEmpty(msgstr))
                {
                    msgstr += extracted;
                }
                else
                {
                    msgid += extracted;
                }
            }

            if (!String.IsNullOrEmpty(msgid) && !String.IsNullOrEmpty(msgstr))
            {
                _translations[msgid] = msgstr;
            }
            else if (!String.IsNullOrEmpty(msgid) && msgstrPlural.Count > 0)
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
    /// Gets the translated string for a given TransportProtocol.
    /// </summary>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if available, otherwise returns the original text.</returns>
    public String GetString(String id)
        => _translations.TryGetValue(id, out String? value) ? value : id;

    /// <summary>
    /// Gets the pluralized translation for a given TransportProtocol.
    /// </summary>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The TransportProtocol to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    public String GetPluralString(String id, String idPlural, Int32 n)
    {
        if (_pluralTranslations.TryGetValue(id, out String[]? plurals))
        {
            Int32 index = _pluralRule(n);
            if (index >= 0 && index < plurals.Length)
            {
                return FormatPlaceholders(plurals[index], n);
            }
        }
        return n == 1 ? id : idPlural;
    }

    /// <summary>
    /// Retrieves a translation with context.
    /// </summary>
    /// <param name="context">Context to distinguish similar translations.</param>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if found, otherwise returns the original text.</returns>
    public String GetParticularString(String context, String id)
    {
        String key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context
        return _translations.TryGetValue(key, out String? value) ? value : id;
    }

    /// <summary>
    /// Retrieves a pluralized translation with context.
    /// </summary>
    /// <param name="context">Context to distinguish similar translations.</param>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The TransportProtocol to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    public String GetParticularPluralString(String context, String id, String idPlural, Int32 n)
    {
        String key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context

        if (_pluralTranslations.TryGetValue(key, out String[]? plurals))
        {
            Int32 index = _pluralRule(n);

            if (index >= 0 && index < plurals.Length)
            {
                return FormatPlaceholders(plurals[index], n);
            }
        }

        return n == 1 ? id : idPlural;
    }

    /// <summary>
    /// Retrieves metadata value from the PO file.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The metadata value if found, otherwise <c>null</c>.</returns>
    public String? GetMetadata(String key)
        => _metadata.TryGetValue(key, out String? value) ? value : null;

    #endregion Public API

    #region Private Methods

    // Add this helper method to format strings consistently
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static String FormatPlaceholders(String format, Int32 n) => String.IsNullOrEmpty(format) ? format : format.Replace("%d", n.ToString());

    /// <summary>
    /// Extracts quoted value from a line.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static String ExtractQuotedValue(String line)
    {
        Match match = ExtractQuotedText().Match(line);
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Extracts plural index from msgstr[N].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int32 ExtractPluralIndex(String line)
    {
        Match match = ExtractPluralIndex().Match(line);
        return match.Success ? Int32.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Ensures the plural list has enough size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsurePluralListSize(List<String> list, Int32 index)
    {
        while (list.Count <= index)
        {
            list.Add("");
        }
    }

    /// <summary>
    /// Parses metadata from the PO file.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ParseMetadata(String metadata)
    {
        foreach (String line in metadata.Split("\\n"))
        {
            String[] parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                _metadata[parts[0].Trim()] = parts[1].Trim();
            }
        }

        // Set plural rule if available
        if (_metadata.TryGetValue("Plural-Forms", out String? pluralForms))
        {
            _pluralRule = ParsePluralRule(pluralForms);
        }
    }

    /// <summary>
    /// Parses the plural rule from PO file metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<Int32, Int32> ParsePluralRule(String rule)
    {
        Dictionary<String, Func<Int32, Int32>> rules = new()
        {
            { "nplurals=1; plural=0;", n => 0 },                     // Japanese, Chinese, Vietnam
            { "nplurals=2; plural=(n != 1);", n => n == 1 ? 0 : 1 }, // English, Spanish, Italian
            { "nplurals=2; plural=(n > 1);", n => n > 1 ? 1 : 0 }    // French, Portuguese
        };

        foreach (var kvp in rules)
        {
            if (rule.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return n => n == 1 ? 0 : 1;
    }

    #endregion Private Methods

    #region Generated Regex Patterns

    [GeneratedRegex(@"msgstr\[(\d+)\]")]
    private static partial Regex ExtractPluralIndex();

    [GeneratedRegex("\"(.*?)\"")]
    private static partial Regex ExtractQuotedText();

    #endregion Generated Regex Patterns
}