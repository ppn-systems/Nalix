namespace Nalix.Serialization.Formatters;

/// <summary>
/// Global registry to register and retrieve formatters without boxing.
/// </summary>
internal static class FormatterProvider
{
    static FormatterProvider()
    {
        // Others
        Register<System.Char>(new UnmanagedFormatter<System.Char>());
        Register<System.Boolean>(new UnmanagedFormatter<System.Boolean>());

        // Integer types
        Register<System.SByte>(new UnmanagedFormatter<System.SByte>());
        Register<System.Byte>(new UnmanagedFormatter<System.Byte>());
        Register<System.Int16>(new UnmanagedFormatter<System.Int16>());
        Register<System.UInt16>(new UnmanagedFormatter<System.UInt16>());
        Register<System.Int32>(new UnmanagedFormatter<System.Int32>());
        Register<System.UInt32>(new UnmanagedFormatter<System.UInt32>());
        Register<System.Int64>(new UnmanagedFormatter<System.Int64>());
        Register<System.UInt64>(new UnmanagedFormatter<System.UInt64>());

        // Floating point
        Register<System.Single>(new UnmanagedFormatter<System.Single>()); // float
        Register<System.Double>(new UnmanagedFormatter<System.Double>());
        Register<System.Decimal>(new UnmanagedFormatter<System.Decimal>());
    }

    public static void Register<T>(IFormatter<T> formatter)
        => FormatterCache<T>.Formatter = formatter
        ?? throw new System.ArgumentNullException(nameof(formatter));

    public static IFormatter<T> Get<T>()
        => FormatterCache<T>.Formatter
        ?? throw new System.InvalidOperationException($"No formatter registered for type {typeof(T)}.");
}
