using Nalix.Common.Connection;
using Nalix.Common.Package.Attributes;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static PacketDescriptor GetPacketAttributes(System.Reflection.MethodInfo method)
        => _attributeCache.GetOrAdd(method, static m => new PacketDescriptor(
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m)!,
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m)
        ));

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void Failure(System.Type returnType, System.Exception ex)
        => _logger?.Error($"Handler failed: {returnType.Name} - {ex.Message}");

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Threading.Tasks.ValueTask<System.Boolean> DispatchPacketAsync(
        TPacket packet, IConnection connection)
        => new(connection.Tcp.SendAsync(packet));

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