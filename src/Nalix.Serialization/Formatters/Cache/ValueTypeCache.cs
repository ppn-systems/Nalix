namespace Nalix.Serialization.Formatters.Cache;

/// <summary>
/// Provides a static cache for storing and retrieving formatters for value types (structs).
/// </summary>
/// <typeparam name="T">The value type (struct) for which the formatter is stored.</typeparam>
internal class ValueTypeCache<T> where T : struct
{
    /// <summary>
    /// The cached formatter instance for the specified value type <typeparamref name="T"/>.
    /// </summary>
    public static IFormatter<T> Formatter;
}
