using System.Diagnostics;

namespace Notio.Network.Package;

[DebuggerDisplay("Packet {Id}: Id={Id}, Type={Type}, Number={Number}, Length={Length}")]
public readonly partial struct Packet
{
    /// <summary>
    /// Converts the packet's data into a human-readable, detailed string representation.
    /// </summary>
    /// <remarks>
    /// This method provides a structured view of the packet's contents, including:
    /// - Number, type, flags, Number, priority, timestamp, and checksum.
    /// - Payload size and, if applicable, a hex dump of the payload data.
    /// - If the payload is larger than 32 bytes, only the first and last 16 bytes are displayed.
    /// </remarks>
    /// <returns>
    /// A formatted string containing detailed packet information.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string ToDetailedString()
    {
        System.Text.StringBuilder sb = new();

        sb.AppendLine($"Packet [{Id}]:");
        sb.AppendLine($"  Code: {Code}");
        sb.AppendLine($"  Type: {Type}");
        sb.AppendLine($"  Flags: {Flags}");
        sb.AppendLine($"  Number: 0x{Number:X4}");
        sb.AppendLine($"  Priority: {Priority}");
        sb.AppendLine($"  Timestamp: {Timestamp}");
        sb.AppendLine($"  Checksum: 0x{Checksum:X8} (Valid: {IsValid()})");
        sb.AppendLine($"  Payload: {Payload.Length} bytes");

        if (Payload.Length > 0)
        {
            sb.Append("  Data: ");

            if (Payload.Length <= 32)
                for (int i = 0; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            else
            {
                for (int i = 0; i < 16; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");

                sb.Append("... ");

                for (int i = Payload.Length - 16; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a string representation of this packet for debugging purposes.
    /// </summary>
    /// <returns>A string that represents this packet.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => $"Packet Number={Number}, Type={Type}, Number={Id}, " +
           $"Flags={Flags}, Priority={Priority}, Timestamp={Timestamp}, " +
           $"Checksum={IsValid()}, Payload={Payload.Length} bytes, Size={Length} bytes";
}
