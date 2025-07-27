namespace Nalix.SDK.L10N;

/// <summary>
/// Provides localization support for applications using Portable Object (PO) files.
/// This class offers a global access point for translations based on the server's
/// default language.
/// </summary>
/// <remarks>
/// The <see cref="Localization"/> class allows retrieving translations for singular
/// and plural forms, with or without contextual information.
/// </remarks>
public static class Localization
{
    #region Fields

    private static Localizer Localizer = new();

    #endregion Fields

    #region Public API

    /// <summary>
    /// Sets the global localizer instance to be used for translations.
    /// </summary>
    /// <param name="localizer">The <see cref="Localizer"/> instance containing translation data.</param>
    /// <remarks>
    /// This method is intended for internal use and should not be called directly.
    /// </remarks>
    public static void SetLocalizer(Localizer localizer)
        => Localizer = localizer;

    /// <summary>
    /// Retrieves the translated string corresponding to the given identifier.
    /// </summary>
    /// <param name="id">The identifier of the string to be translated.</param>
    /// <returns>
    /// The translated string if available; otherwise, returns <paramref name="id"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = Localization.Get("hello");
    /// </code>
    /// </example>
    public static System.String Get(System.String id)
        => Localizer.Get(id);

    /// <summary>
    /// Retrieves the translated string for a specific context.
    /// </summary>
    /// <param name="context">The context in which the translation is used.</param>
    /// <param name="id">The identifier of the string to be translated.</param>
    /// <returns>
    /// The translated string if available; otherwise, returns <paramref name="id"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = Localization.GetParticular("menu", "File");
    /// </code>
    /// </example>
    public static System.String GetParticular(System.String context, System.String id)
        => Localizer.GetParticular(context, id);

    /// <summary>
    /// Retrieves the translated string in singular or plural form based on the given count.
    /// </summary>
    /// <param name="id">The singular form of the identifier.</param>
    /// <param name="idPlural">The plural form of the identifier.</param>
    /// <param name="n">The count determining whether singular or plural should be used.</param>
    /// <returns>
    /// The translated singular or plural string based on <paramref name="n"/>.
    /// If no translation is available, returns <paramref name="id"/> or <paramref name="idPlural"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = Localization.GetPlural("apple", "apples", 5);
    /// </code>
    /// </example>
    public static System.String GetPlural(System.String id, System.String idPlural, System.Int32 n)
        => Localizer.GetPlural(id, idPlural, n);

    /// <summary>
    /// Retrieves the translated string in singular or plural form for a specific context.
    /// </summary>
    /// <param name="context">The context in which the translation is used.</param>
    /// <param name="id">The singular form of the identifier.</param>
    /// <param name="idPlural">The plural form of the identifier.</param>
    /// <param name="n">The count determining whether singular or plural should be used.</param>
    /// <returns>
    /// The translated singular or plural string based on <paramref name="n"/>.
    /// If no translation is available, returns <paramref name="id"/> or <paramref name="idPlural"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// string translated = Localization.GetParticularPlural("inventory", "item", "items", 3);
    /// </code>
    /// </example>
    public static System.String GetParticularPlural(System.String context, System.String id, System.String idPlural, System.Int32 n)
        => Localizer.GetParticularPlural(context, id, idPlural, n);

    #endregion Public API
}
