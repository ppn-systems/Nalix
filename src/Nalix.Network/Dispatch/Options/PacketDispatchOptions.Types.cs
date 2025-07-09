using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Performance metrics structure
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    [method: System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private readonly struct MetricsData(
        System.Int64 elapsedTicks,
        System.String handlerName,
        System.Boolean isEnabled)
    {
        public readonly System.Boolean IsEnabled = isEnabled;
        public readonly System.String HandlerName = handlerName;
        public readonly System.Int64 ElapsedTicks = elapsedTicks;
    }

    /// <summary>
    /// Optimized packet descriptor với struct layout cho performance
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    [method: System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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