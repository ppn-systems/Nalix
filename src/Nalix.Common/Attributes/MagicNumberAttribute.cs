// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Messaging.Protocols;

namespace Nalix.Common.Attributes;

/// <summary>
/// Attribute to specify the MagicNumber for a packet type.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MagicNumberAttribute(System.UInt32 magicNumber) : System.Attribute
{
    /// <summary>
    /// Gets the magic number associated with the packet type.
    /// </summary>
    public System.UInt32 MagicNumber { get; } = magicNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagicNumberAttribute"/> class using a <see cref="ProtocolMagic"/> value.
    /// </summary>
    /// <param name="magicNumber">The <see cref="ProtocolMagic"/> value to associate with the packet type.</param>
    public MagicNumberAttribute(ProtocolMagic magicNumber) : this((System.UInt32)magicNumber)
    {
        // Default constructor for cases where no magic number is specified.
    }
}
