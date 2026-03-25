// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;

namespace Nalix.SDK.L10N.Formats;

/// <summary>
/// Represents a Portable Object (PO) file for translations.
/// </summary>
[System.ComponentModel.EditorBrowsable(
    System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Translations={_translations.Count}, Plurals={_pluralTranslations.Count}")]
public class PoFile
{
    #region Fields

    private readonly Dictionary<string, string> _metadata = [];
    private readonly Dictionary<string, string> _translations = [];
    private readonly Dictionary<string, string[]> _pluralTranslations = [];

    /// <summary>
    /// Standard rule (English)
    /// </summary>
    private Func<int, int> _pluralRule = n => n == 1 ? 0 : 1;

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
    public PoFile(string path) => LoadFromFile(path);

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Loads a PO file from the specified path and parses its contents.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        using StreamReader reader = new(path, System.Text.Encoding.UTF8);
        Parse(reader);
    }

    /// <summary>
    /// Parses the PO file content from a <see cref="StreamReader"/>.
    /// </summary>
    /// <param name="reader">A <see cref="StreamReader"/> containing PO file content.</param>
    private void Parse(StreamReader reader)
    {
        string? line;
        string msgid = "", msgstr = "";
        List<string> msgstrPlural = [];
        bool isPlural = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
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
        if (_translations.TryGetValue("", out string? metadata))
        {
            ParseMetadata(metadata);
        }
    }

    /// <summary>
    /// Gets the translated string for a given ProtocolType.
    /// </summary>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if available, otherwise returns the original text.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string GetString(string id)
        => _translations.TryGetValue(id, out string? value) ? value : id;

    /// <summary>
    /// Gets the pluralized translation for a given ProtocolType.
    /// </summary>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The ProtocolType to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string GetPluralString(string id, string idPlural, int n)
    {
        if (_pluralTranslations.TryGetValue(id, out string[]? plurals))
        {
            int index = _pluralRule(n);
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
    /// <param name="context">Options to distinguish similar translations.</param>
    /// <param name="id">The original text to translate.</param>
    /// <returns>The translated string if found, otherwise returns the original text.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string GetParticularString(string context, string id)
    {
        string key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context
        return _translations.TryGetValue(key, out string? value) ? value : id;
    }

    /// <summary>
    /// Retrieves a pluralized translation with context.
    /// </summary>
    /// <param name="context">Options to distinguish similar translations.</param>
    /// <param name="id">Singular form of the string.</param>
    /// <param name="idPlural">Plural form of the string.</param>
    /// <param name="n">The ProtocolType to determine the plural form.</param>
    /// <returns>The correctly pluralized translation if available, otherwise returns the best available fallback.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string GetParticularPluralString(
        string context, string id, string idPlural, int n)
    {
        string key = $"{context}\u0004{id}"; // PO uses \u0004 to separate context

        if (_pluralTranslations.TryGetValue(key, out string[]? plurals))
        {
            int index = _pluralRule(n);

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
    public string? GetMetadata(string key)
        => _metadata.TryGetValue(key, out string? value) ? value : null;

    #endregion Public API

    #region Private Methods

    /// <summary>
    /// Push this helper method to format strings consistently
    /// </summary>
    /// <param name="format"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string FormatPlaceholders(string format, int n)
        => string.IsNullOrEmpty(format) ? format : format.Replace("%d", n.ToString());

    /// <summary>
    /// Extracts quoted value from a line.
    /// </summary>
    /// <param name="line"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string ExtractQuotedValue(string line)
    {
        int start = line.IndexOf('"');
        if (start < 0)
        {
            return string.Empty;
        }

        int end = line.IndexOf('"', start + 1);
        return end < 0 ? string.Empty : line.Substring(start + 1, end - start - 1);
    }


    /// <summary>
    /// Extracts plural index from msgstr[N].
    /// </summary>
    /// <param name="line"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int ExtractPluralIndex(string line)
    {
        const string marker = "msgstr[";
        int start = line.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return 0;
        }

        start += marker.Length;
        int end = line.IndexOf(']', start);
        if (end < 0)
        {
            return 0;
        }
        else
        {
            return int.TryParse(line
                                                    .AsSpan(start, end - start), out int value)
                ? value
                : 0;
        }
    }

    /// <summary>
    /// Ensures the plural list has enough size.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="index"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EnsurePluralListSize(List<string> list, int index)
    {
        while (list.Count <= index)
        {
            list.Add("");
        }
    }

    /// <summary>
    /// Parses metadata from the PO file.
    /// </summary>
    /// <param name="metadata"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ParseMetadata(string metadata)
    {
        foreach (string line in metadata.Split("\\n"))
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                _metadata[parts[0].Trim()] = parts[1].Trim();
            }
        }

        // Set plural rule if available
        if (_metadata.TryGetValue("Plural-Forms", out string? pluralForms))
        {
            _pluralRule = ParsePluralRule(pluralForms);
        }
    }

    /// <summary>
    /// Parses the plural rule from PO file metadata.
    /// </summary>
    /// <param name="rule"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Func<int, int> ParsePluralRule(string rule)
    {
        Dictionary<
            string, Func<int, int>> rules = new()
        {
            { "nplurals=1; plural=0;", n => 0 },                     // Japanese, Chinese, Vietnam
            { "nplurals=2; plural=(n != 1);", n => n == 1 ? 0 : 1 }, // English, Spanish, Italian
            { "nplurals=2; plural=(n > 1);", n => n > 1 ? 1 : 0 }    // French, Portuguese
        };

        foreach (KeyValuePair<string, Func<int, int>> kvp in rules)
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
