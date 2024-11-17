using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for reading header values from serialized packet data using unsafe code for performance.
/// </summary>
public static class HeaderExtensions
{
    /// <summary>
    /// Reads the OpCode value from the header of an IPacket.
    /// </summary>
    /// <param name="buffer">The input buffer containing the serialized packet data.</param>
    /// <returns>The OpCode value of the packet.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the packet header.</exception>
    public static unsafe System.UInt16 ReadOpCode(this System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.Length < 6) // Ensure the buffer is large enough to contain the header
        {
            throw new System.ArgumentException("Buffer is too small to contain the IPacket header.");
        }

        // Use unsafe code to directly access memory and read OpCode from byte 4 and 5
        fixed (System.Byte* pBuffer = buffer)
        {
            return *(System.UInt16*)(pBuffer + 4); // OpCode starts from byte 4
        }
    }

    /// <summary>
    /// Reads the MagicNumber value from the header of an IPacket.
    /// </summary>
    /// <param name="buffer">The input buffer containing the serialized packet data.</param>
    /// <returns>The MagicNumber value of the packet.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the MagicNumber.</exception>
    public static unsafe System.UInt32 ReadMagicNumber(this System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.Length < 4) // Ensure the buffer is large enough to contain the MagicNumber
        {
            throw new System.ArgumentException("Buffer is too small to contain the MagicNumber.");
        }

        // Use unsafe code to directly access memory and read MagicNumber from the first 4 bytes
        fixed (System.Byte* pBuffer = buffer)
        {
            return *(System.UInt32*)pBuffer; // MagicNumber is stored in the first 4 bytes
        }
    }

    /// <summary>
    /// Reads the Flags value from the header of an IPacket.
    /// </summary>
    /// <param name="buffer">The input buffer containing the serialized packet data.</param>
    /// <returns>The Flags value of the packet.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the Flags.</exception>
    public static unsafe PacketFlags ReadFlags(this System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.Length < 6) // Ensure the buffer is large enough to contain the Flags
        {
            throw new System.ArgumentException("Buffer is too small to contain the Flags.");
        }

        // Use unsafe code to directly access memory and read Flags from byte 6 and 7
        fixed (System.Byte* pBuffer = buffer)
        {
            return (PacketFlags)(*(System.UInt16*)(pBuffer + 6)); // Flags are stored at byte 6 and 7
        }
    }

    /// <summary>
    /// Reads the Priority value from the header of an IPacket.
    /// </summary>
    /// <param name="buffer">The input buffer containing the serialized packet data.</param>
    /// <returns>The Priority value of the packet.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the Priority.</exception>
    public static unsafe PacketPriority ReadPriority(this System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.Length < 8) // Ensure the buffer is large enough to contain the Priority
        {
            throw new System.ArgumentException("Buffer is too small to contain the Priority.");
        }

        // Use unsafe code to directly access memory and read Priority from byte 8 and 9
        fixed (System.Byte* pBuffer = buffer)
        {
            return (PacketPriority)(*(System.UInt16*)(pBuffer + 8)); // Priority is stored at byte 8 and 9
        }
    }

    /// <summary>
    /// Reads the Transport value from the header of an IPacket.
    /// </summary>
    /// <param name="buffer">The input buffer containing the serialized packet data.</param>
    /// <returns>The Transport value of the packet.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the buffer is too small to contain the Transport value.</exception>
    public static unsafe TransportProtocol ReadTransport(this System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.Length < 10) // Ensure the buffer is large enough to contain the Transport
        {
            throw new System.ArgumentException("Buffer is too small to contain the Transport.");
        }

        // Use unsafe code to directly access memory and read Transport from byte 10 and 11
        fixed (System.Byte* pBuffer = buffer)
        {
            return (TransportProtocol)(*(System.UInt16*)(pBuffer + 10)); // Transport is stored at byte 10 and 11
        }
    }
}
