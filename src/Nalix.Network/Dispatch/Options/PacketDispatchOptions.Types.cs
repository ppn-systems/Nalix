using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Performance metrics structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly struct MetricsData(long elapsedTicks, string handlerName, bool isEnabled)
    {
        public readonly bool IsEnabled = isEnabled;
        public readonly long ElapsedTicks = elapsedTicks;
        public readonly string HandlerName = handlerName;
    }

    /// <summary>
    /// Optimized packet descriptor với struct layout cho performance
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly struct PacketDescriptor(
        PacketOpcodeAttribute opCode,
        PacketTimeoutAttribute? timeout,
        PacketRateLimitAttribute? rateLimit,
        PacketPermissionAttribute? permission,
        PacketEncryptionAttribute? encryption)
    {
        public readonly PacketOpcodeAttribute OpCode = opCode;
        public readonly PacketTimeoutAttribute? Timeout = timeout;
        public readonly PacketRateLimitAttribute? RateLimit = rateLimit;
        public readonly PacketPermissionAttribute? Permission = permission;
        public readonly PacketEncryptionAttribute? Encryption = encryption;
    }
}