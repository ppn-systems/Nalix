using Nalix.Common.Connection;
using Nalix.Common.Package.Attributes;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketDescriptor GetPacketAttributes(MethodInfo method)
        => _attributeCache.GetOrAdd(method, static m => new PacketDescriptor(
            m.GetCustomAttribute<PacketOpcodeAttribute>()!,
            m.GetCustomAttribute<PacketTimeoutAttribute>(),
            m.GetCustomAttribute<PacketRateLimitAttribute>(),
            m.GetCustomAttribute<PacketPermissionAttribute>(),
            m.GetCustomAttribute<PacketEncryptionAttribute>()
        ));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Failure(Type returnType, Exception ex)
        => _logger?.Error("Handler failed: {0} - {1}", returnType.Name, ex.Message);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ValueTask<bool> DispatchPacketAsync(TPacket packet, IConnection connection)
        => new(connection.Tcp.SendAsync(packet));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool CheckRateLimit(ReadOnlySpan<char> remoteEndPoint, in PacketDescriptor attributes)
        => attributes.RateLimit is null || _rateLimiter.Check(remoteEndPoint.ToString(), attributes.RateLimit);
}