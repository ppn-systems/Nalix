using System;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Provides packet discovery, metadata inspection, and runtime packet creation services.
/// </summary>
public interface IPacketCatalogService
{
    /// <summary>
    /// Gets the loaded packet catalog.
    /// </summary>
    PacketCatalog Catalog { get; }

    /// <summary>
    /// Loads packet types from a user-selected assembly and refreshes the runtime catalog.
    /// </summary>
    /// <param name="assemblyPath">The full assembly path.</param>
    /// <returns>The refreshed packet catalog.</returns>
    PacketCatalog LoadPacketAssembly(string assemblyPath);

    /// <summary>
    /// Finds a packet descriptor by concrete type.
    /// </summary>
    /// <param name="packetType">The packet type to find.</param>
    /// <returns>The matching descriptor when found.</returns>
    PacketTypeDescriptor? FindByType(Type packetType);

    /// <summary>
    /// Creates a packet instance for the specified descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor to instantiate.</param>
    /// <returns>The created packet.</returns>
    IPacket CreatePacket(PacketTypeDescriptor descriptor);

    /// <summary>
    /// Deserializes a packet from raw bytes.
    /// </summary>
    /// <param name="rawBytes">The packet bytes.</param>
    /// <returns>The deserialized packet.</returns>
    IPacket Deserialize(byte[] rawBytes);
}
