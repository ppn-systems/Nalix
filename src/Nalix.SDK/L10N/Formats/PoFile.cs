// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.SDK.L10N.Formats;

/// <summary>
/// Represents a Portable Object (PO) file for translations.
/// </summary>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Translations={_translations.Count}, Plurals={_pluralTranslations.Count}")]
public partial class PoFile
{
    #region Fields

    private readonly System.Collections.Generic.Dictionary<System.String, System.String> _metadata = [];
    private readonly System.Collections.Generic.Dictionary<System.String, System.String> _translations = [];
    private readonly System.Collections.Generic.Dictionary<System.String, System.String[]> _pluralTranslations = [];

    private System.Func<System.Int32, System.Int32> _pluralRule = n => n == 1 ? 0 : 1; // Standard rule (English)

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
    /// <exception cref="System.IO.FileNotFoundException">Thrown if the specified file does not exist.</exception>
    public PoFile(System.String path) => LoadFromFile(path);

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Loads a PO file from the specified path and parses its contents.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="System.IO.FileNotFoundException">Thrown if the file does not exist.</exception>
    public void LoadFromFile(System.String path)
    {
        if (!System.IO.File.Exists(path))
        {
            throw new System.IO.FileNotFoundException($"File not found: {path}");
        }

        using var reader = new System.IO.StreamReader(path, System.Text.Encoding.UTF8);
        Parse(reader);
    }

    /// <summary>
    /// Parses the PO file content from a <see cref="System.IO.StreamReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="System.IO.StreamReader"/> containing PO file content.</param>
    private void Parse(System.IO.StreamReader reader)
    {
        System.String line;
        System.String msgid = "", msgstr = "";
        System.Collections.Generic.List<System.String> msgstrPlural = [];
        System.Boolean isPlural = false;

        while ((line = reader.ReadLine()) != null)
        {
            System.String trimmed = line.Trim();
            if (System.String.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
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
                _ = ExtractQuotedValue(trimmed);
                isPlural = true;
            }
            else if (trimmed.StartsWith("msgstr["))
            {
                System.Int32 index = ExtractPluralIndex(trimmed);
                EnsurePluralListSize(msgstrPlural, index);
                msgstrPlural[index] = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith("msgstr "))
            {
                msgstr = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith('\"')) // Multiline string continuation
            {
                System.String extracted = ExtractQuotedValue(trimmed);

                if (isPlural && msgstrPlural.Count > 0)
                {
                    msgstrPlural[^1] += extracted;
                }
                else if (!System.String.IsNullOrEmpty(msgstr))
                {
                    msgstr += extracted;
                }
                else
                {
                    msgid += extracted;
                }
            }

            if (!System.String.IsNullOrEmpty(msgid) && !System.String.IsNullOrEmpty(msgstr))
            {
                _translations[msgid] = msgstr;
            }
            else if (!System.String.IsNullOrEmpty(msgid) && msgstrPlural.Count > 0)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetString(System.String id)
        => _translations.TryGetValue(id, out System.String value) ? value : id;

    /// <summary>
    /// Gets the pluralized translation for a given TransportProtocol.
    /// </summary>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The TransportProtocol to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetPluralString(System.String id, System.String idPlural, System.Int32 n)
    {
        if (_pluralTranslations.TryGetValue(id, out System.String[] plurals))
        {
            System.Int32 index = _pluralRule(n);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetParticularString(System.String context, System.String id)
    {
        System.String key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context
        return _translations.TryGetValue(key, out System.String value) ? value : id;
    }

    /// <summary>
    /// Retrieves a pluralized translation with context.
    /// </summary>
    /// <param name="context">Context to distinguish similar translations.</param>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The TransportProtocol to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetParticularPluralString(
        System.String context, System.String id, System.String idPlural, System.Int32 n)
    {
        System.String key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context

        if (_pluralTranslations.TryGetValue(key, out System.String[] plurals))
        {
            System.Int32 index = _pluralRule(n);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String GetMetadata(System.String key)
        => _metadata.TryGetValue(key, out System.String value) ? value : null;

    #endregion Public API

    #region Private Methods

    // Push this helper method to format strings consistently
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String FormatPlaceholders(System.String format, System.Int32 n)
        => System.String.IsNullOrEmpty(format) ? format : format.Replace("%d", n.ToString());

    /// <summary>
    /// Extracts quoted value from a line.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String ExtractQuotedValue(System.String line)
    {
        System.Int32 start = line.IndexOf('"');
        if (start < 0)
        {
            return System.String.Empty;
        }

        System.Int32 end = line.IndexOf('"', start + 1);
        return end < 0 ? System.String.Empty : line.Substring(start + 1, end - start - 1);
    }


    /// <summary>
    /// Extracts plural index from msgstr[N].
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 ExtractPluralIndex(System.String line)
    {
        const System.String marker = "msgstr[";
        System.Int32 start = line.IndexOf(marker, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return 0;
        }

        start += marker.Length;
        System.Int32 end = line.IndexOf(']', start);
        return end < 0 ? 0 : System.Int32.TryParse(System.MemoryExtensions
                                        .AsSpan(line, start, end - start), out System.Int32 value) ? value : 0;
    }

    /// <summary>
    /// Ensures the plural list has enough size.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EnsurePluralListSize(System.Collections.Generic.List<System.String> list, System.Int32 index)
    {
        while (list.Count <= index)
        {
            list.Add("");
        }
    }

    /// <summary>
    /// Parses metadata from the PO file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ParseMetadata(System.String metadata)
    {
        foreach (System.String line in metadata.Split("\\n"))
        {
            System.String[] parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                _metadata[parts[0].Trim()] = parts[1].Trim();
            }
        }

        // Set plural rule if available
        if (_metadata.TryGetValue("Plural-Forms", out System.String pluralForms))
        {
            _pluralRule = ParsePluralRule(pluralForms);
        }
    }

    /// <summary>
    /// Parses the plural rule from PO file metadata.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Int32, System.Int32> ParsePluralRule(System.String rule)
    {
        System.Collections.Generic.Dictionary<
            System.String, System.Func<System.Int32, System.Int32>> rules = new()
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
}