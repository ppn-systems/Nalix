namespace Nalix.Serialization.Formatters.Cache;

/// <summary>
/// Provides a static cache for storing and retrieving formatters for specific types.
/// </summary>
/// <typeparam name="T">The type for which the formatter is stored.</typeparam>
internal class ComplexTypeCache<T>
{
    /// <summary>
    /// The cached formatter instance for the specified type <typeparamref name="T"/>.
    /// </summary>
    public static IFormatter<T> Class;

    /// <summary>
    /// The cached formatter instance for the specified type <typeparamref name="T"/>.
    /// </summary>
    public static IFormatter<T> Struct;
}
