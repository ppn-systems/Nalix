using System;
using System.Collections.Generic;
using System.IO;

namespace Notio.Shared.L10N;

/// <summary>
/// Manages multiple language-specific localizers for string translation.
/// </summary>
/// <remarks>
/// This class allows loading multiple localization files (PO files) 
/// for different languages and retrieving translations based on the specified language.
/// </remarks>
public sealed class MultiLocalizer
{
    private Localizer _defaultLocalizer = new();
    private readonly Dictionary<string, Localizer> _localizers = [];

    /// <summary>
    /// Loads a PO file for a specified language and creates a corresponding localizer.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "en", "fr").</param>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the specified PO file does not exist.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown if the PO file is invalid or cannot be parsed.
    /// </exception>
    /// <example>
    /// <code>
    /// var multiLocalizer = new MultiLocalizer();
    /// multiLocalizer.Load("en", "localization_en.po");
    /// multiLocalizer.Load("fr", "localization_fr.po");
    /// </code>
    /// </example>
    public void Load(string languageName, string path)
    {
        languageName = languageName.ToLower();

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        lock (_localizers)
            _localizers[languageName] = new Localizer(path);
    }

    /// <summary>
    /// Determines whether a localizer for the specified language exists.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "en", "fr").</param>
    /// <returns>
    /// <c>true</c> if the language is loaded; otherwise, <c>false</c>.
    /// </returns>
    /// <example>
    /// <code>
    /// bool exists = multiLocalizer.Contains("fr");
    /// </code>
    /// </example>
    public bool Contains(string languageName)
    {
        languageName = languageName.ToLower();

        lock (_localizers)
            return _localizers.ContainsKey(languageName);
    }

    /// <summary>
    /// Retrieves the localizer for a specified language.
    /// If the language is not found, the default localizer is returned.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "en", "fr").</param>
    /// <returns>
    /// The corresponding <see cref="Localizer"/> instance, or the default localizer if the language is not found.
    /// </returns>
    /// <example>
    /// <code>
    /// Localizer localizer = multiLocalizer.Get("fr");
    /// </code>
    /// </example>
    public Localizer Get(string languageName)
    {
        languageName = languageName.ToLower();

        lock (_localizers)
        {
            if (!_localizers.TryGetValue(languageName, out var localizer))
                return _defaultLocalizer;

            return localizer;
        }
    }

    /// <summary>
    /// Attempts to retrieve a localizer for a specified language.
    /// If the language is not found, the method returns <c>false</c> and outputs the default localizer.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "en", "fr").</param>
    /// <param name="localizer">
    /// The retrieved <see cref="Localizer"/> instance, or the default localizer if not found.
    /// </param>
    /// <returns><c>true</c> if the language is found; otherwise, <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (multiLocalizer.TryGet("es", out var localizer))
    /// {
    ///     string translated = localizer.Get("hello");
    /// }
    /// </code>
    /// </example>
    public bool TryGet(string languageName, out Localizer localizer)
    {
        languageName = languageName.ToLower();

        lock (_localizers)
        {
            if (_localizers.TryGetValue(languageName, out var foundLocalizer) &&
                foundLocalizer != null)
            {
                localizer = foundLocalizer;
                return true;
            }

            localizer = _defaultLocalizer;
        }

        return false;
    }

    /// <summary>
    /// Retrieves a list of all loaded language names.
    /// </summary>
    /// <returns>An array of loaded language names.</returns>
    /// <example>
    /// <code>
    /// string[] languages = multiLocalizer.GetLanguages();
    /// </code>
    /// </example>
    public string[] GetLanguages()
    {
        lock (_localizers)
            return [.. _localizers.Keys];
    }

    /// <summary>
    /// Sets the default localizer to use when a language-specific localizer is unavailable.
    /// </summary>
    /// <param name="languageName">The language identifier (e.g., "en", "fr").</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the specified language does not exist in the loaded localizers.
    /// </exception>
    /// <example>
    /// <code>
    /// multiLocalizer.SetDefault("fr");
    /// </code>
    /// </example>
    public void SetDefault(string languageName)
    {
        var loweredName = languageName.ToLower();

        lock (_localizers)
        {
            if (!_localizers.TryGetValue(loweredName, out var localizer))
                throw new ArgumentException($"No localizer for language {languageName} found.");

            _defaultLocalizer = localizer;
        }
    }

    /// <summary>
    /// Retrieves the current default localizer.
    /// </summary>
    /// <returns>The default <see cref="Localizer"/> instance.</returns>
    /// <example>
    /// <code>
    /// Localizer defaultLocalizer = multiLocalizer.GetDefault();
    /// </code>
    /// </example>
    public Localizer GetDefault()
    {
        lock (_localizers)
            return _defaultLocalizer;
    }
}
