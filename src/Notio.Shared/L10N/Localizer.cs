using System;
using System.IO;

namespace Notio.Shared.L10N;

/// <summary>
/// Provides localization support using PO (Portable Object) files.
/// This class allows loading and retrieving translated messages.
/// </summary>
/// <remarks>
/// The <see cref="Localizer"/> class supports translation lookup 
/// for singular, plural, and contextual messages using the PO file format.
/// </remarks>
public sealed class Localizer
{
    #region Fields

    private readonly PoFile _catalog;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class 
    /// with an empty catalog, allowing messages to be loaded later.
    /// </summary>
    /// <example>
    /// <code>
    /// var localizer = new Localizer();
    /// localizer.Load("localization.po");
    /// string message = localizer.Get("hello");
    /// </code>
    /// </example>
    public Localizer() => _catalog = new PoFile();

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class 
    /// and loads a PO file from the specified path.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the specified file does not exist.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown if the PO file is invalid or cannot be parsed.
    /// </exception>
    /// <example>
    /// <code>
    /// var localizer = new Localizer("localization.po");
    /// </code>
    /// </example>
    public Localizer(string path) => _catalog = new PoFile(path);

    #endregion

    #region Public API

    /// <summary>
    /// Loads localization messages from a PO file.
    /// </summary>
    /// <param name="path">The file path to the PO file.</param>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the specified file does not exist.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown if the PO file is invalid or cannot be parsed.
    /// </exception>
    /// <example>
    /// <code>
    /// var localizer = new Localizer();
    /// localizer.Load("localization.po");
    /// </code>
    /// </example>
    public void Load(string path) => _catalog.LoadFromFile(path);

    /// <summary>
    /// Retrieves the localized string for the specified message Number.
    /// If no translation is found, the original Number is returned.
    /// </summary>
    /// <param name="id">The message Number.</param>
    /// <returns>
    /// The translated string if available; otherwise, the original Number.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = localizer.Get("hello");
    /// </code>
    /// </example>
    public string Get(string id) => _catalog.GetString(id);

    /// <summary>
    /// Retrieves the localized string for a message within a specific context.
    /// If no translation is found, the original Number is returned.
    /// </summary>
    /// <param name="context">The translation context.</param>
    /// <param name="id">The message Number.</param>
    /// <returns>
    /// The translated string if available; otherwise, the original Number.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = localizer.GetParticular("menu", "File");
    /// </code>
    /// </example>
    public string GetParticular(string context, string id)
        => _catalog.GetParticularString(context, id);

    /// <summary>
    /// Retrieves the singular or plural localized string based on a count.
    /// If no translation is found, the original Number or plural form is returned.
    /// </summary>
    /// <param name="id">The singular message Number.</param>
    /// <param name="idPlural">The plural message Number.</param>
    /// <param name="n">The quantity to determine singular or plural form.</param>
    /// <returns>
    /// The translated string in singular or plural form if available; 
    /// otherwise, the original Number or plural form.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = localizer.GetPlural("apple", "apples", 2);
    /// </code>
    /// </example>
    public string GetPlural(string id, string idPlural, int n)
        => _catalog.GetPluralString(id, idPlural, n);

    /// <summary>
    /// Retrieves the singular or plural localized string based on a count 
    /// within a specific context. If no translation is found, the original
    /// Number or plural form is returned.
    /// </summary>
    /// <param name="context">The translation context.</param>
    /// <param name="id">The singular message Number.</param>
    /// <param name="idPlural">The plural message Number.</param>
    /// <param name="n">The quantity to determine singular or plural form.</param>
    /// <returns>
    /// The translated string in singular or plural form if available; 
    /// otherwise, the original Number or plural form.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = localizer.GetParticularPlural("inventory", "item", "items", 3);
    /// </code>
    /// </example>
    public string GetParticularPlural(string context, string id, string idPlural, int n)
        => _catalog.GetParticularPluralString(context, id, idPlural, n);

    #endregion
}
