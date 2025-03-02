using Notio.Serialization.Internal.Reflection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;

namespace Notio.Serialization.Internal;

/// <summary>
/// Contains useful constants and definitions.
/// </summary>
internal static class Definitions
{
    /// <summary>
    /// The basic types information.
    /// </summary>
    internal static readonly Lazy<Dictionary<Type, ExtendedTypeInfo>> BasicTypesInfo = new(() =>
        new Dictionary<Type, ExtendedTypeInfo>
        {
            // Non-Nullables
            {typeof(DateTime), new ExtendedTypeInfo<DateTime>()},
            {typeof(byte), new ExtendedTypeInfo<byte>()},
            {typeof(sbyte), new ExtendedTypeInfo<sbyte>()},
            {typeof(int), new ExtendedTypeInfo<int>()},
            {typeof(uint), new ExtendedTypeInfo<uint>()},
            {typeof(short), new ExtendedTypeInfo<short>()},
            {typeof(ushort), new ExtendedTypeInfo<ushort>()},
            {typeof(long), new ExtendedTypeInfo<long>()},
            {typeof(ulong), new ExtendedTypeInfo<ulong>()},
            {typeof(float), new ExtendedTypeInfo<float>()},
            {typeof(double), new ExtendedTypeInfo<double>()},
            {typeof(char), new ExtendedTypeInfo<char>()},
            {typeof(bool), new ExtendedTypeInfo<bool>()},
            {typeof(decimal), new ExtendedTypeInfo<decimal>()},
            {typeof(Guid), new ExtendedTypeInfo<Guid>()},

            // Strings is also considered a basic type (it's the only basic reference type)
            {typeof(string), new ExtendedTypeInfo<string>()},

            // Nullables
            {typeof(DateTime?), new ExtendedTypeInfo<DateTime?>()},
            {typeof(byte?), new ExtendedTypeInfo<byte?>()},
            {typeof(sbyte?), new ExtendedTypeInfo<sbyte?>()},
            {typeof(int?), new ExtendedTypeInfo<int?>()},
            {typeof(uint?), new ExtendedTypeInfo<uint?>()},
            {typeof(short?), new ExtendedTypeInfo<short?>()},
            {typeof(ushort?), new ExtendedTypeInfo<ushort?>()},
            {typeof(long?), new ExtendedTypeInfo<long?>()},
            {typeof(ulong?), new ExtendedTypeInfo<ulong?>()},
            {typeof(float?), new ExtendedTypeInfo<float?>()},
            {typeof(double?), new ExtendedTypeInfo<double?>()},
            {typeof(char?), new ExtendedTypeInfo<char?>()},
            {typeof(bool?), new ExtendedTypeInfo<bool?>()},
            {typeof(decimal?), new ExtendedTypeInfo<decimal?>()},
            {typeof(Guid?), new ExtendedTypeInfo<Guid?>()},

            // Additional Types
            {typeof(TimeSpan), new ExtendedTypeInfo<TimeSpan>()},
            {typeof(TimeSpan?), new ExtendedTypeInfo<TimeSpan?>()},
            {typeof(IPAddress), new ExtendedTypeInfo<IPAddress>()},
        });

    /// <summary>
    /// The MS Windows codepage 1252 encoding used in some legacy scenarios
    /// such as default CSV text encoding from Excel.
    /// </summary>
    public static readonly Encoding Windows1252Encoding;

    /// <summary>
    /// Initializes the <see cref="Definitions"/> class.
    /// </summary>
    static Definitions()
    {
        var currentAnsiEncoding = Encoding.GetEncoding(0);
        try
        {
            Windows1252Encoding = Encoding.GetEncoding(1252);
        }
        catch (ArgumentException)
        {
            // Log exception if necessary
            Windows1252Encoding = currentAnsiEncoding;
        }
    }

    /// <summary>
    /// Contains all basic value types. i.e. excludes string and nullables.
    /// </summary>
    /// <value>
    /// All basic value types.
    /// </value>
    internal static IReadOnlyCollection<Type> AllBasicValueTypes { get; } = new ReadOnlyCollection<Type>(
            [.. BasicTypesInfo.Value.Where(kvp => kvp.Value.IsValueType).Select(kvp => kvp.Key)]);
}
