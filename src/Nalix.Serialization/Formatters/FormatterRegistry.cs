using System;

namespace Nalix.Serialization.Formatters;

/// <summary>
/// Global registry to register and retrieve formatters without boxing.
/// </summary>
internal static class FormatterRegistry
{
    static FormatterRegistry()
    {
        // Others
        Register<Char>(new UnmanagedFormatter<Char>());
        Register<Boolean>(new UnmanagedFormatter<Boolean>());

        // Integer types
        Register<SByte>(new UnmanagedFormatter<SByte>());
        Register<Byte>(new UnmanagedFormatter<Byte>());
        Register<Int16>(new UnmanagedFormatter<Int16>());
        Register<UInt16>(new UnmanagedFormatter<UInt16>());
        Register<Int32>(new UnmanagedFormatter<Int32>());
        Register<UInt32>(new UnmanagedFormatter<UInt32>());
        Register<Int64>(new UnmanagedFormatter<Int64>());
        Register<UInt64>(new UnmanagedFormatter<UInt64>());

        // Floating point
        Register<Single>(new UnmanagedFormatter<Single>()); // float
        Register<Double>(new UnmanagedFormatter<Double>());
        Register<Decimal>(new UnmanagedFormatter<Decimal>());
    }

    public static void Register<T>(IFormatter<T> formatter)
        => FormatterCache<T>.Formatter = formatter
        ?? throw new ArgumentNullException(nameof(formatter));

    public static IFormatter<T> Get<T>()
        => FormatterCache<T>.Formatter
        ?? throw new InvalidOperationException($"No formatter registered for type {typeof(T)}.");
}
