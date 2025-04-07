using Notio.Common.Security;
using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Specifies the compression settings for packet communication methods.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PacketCompressionAttribute : Attribute
{
    /// <summary>
    /// Gets a value indicating whether the packet is compressed.
    /// </summary>
    public bool IsCompressed { get; }

    /// <summary>
    /// Gets the type of compression used.
    /// </summary>
    public CompressionType Compression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCompressionAttribute"/> class.
    /// </summary>
    /// <param name="isCompressed">Indicates whether the packet is compressed.</param>
    /// <param name="compression">The type of compression.</param>
    public PacketCompressionAttribute(bool isCompressed, CompressionType compression = CompressionType.GZip)
    {
        IsCompressed = isCompressed;
        Compression = compression;
    }
}
