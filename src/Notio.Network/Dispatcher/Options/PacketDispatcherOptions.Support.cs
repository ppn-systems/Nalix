using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket>
    where TPacket : IPacket, IPacketCompressor<TPacket>, IPacketEncryptor<TPacket>
{
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    private record PacketAttributes(
        PacketIdAttribute PacketId,
        PacketPermissionAttribute? Permission,
        PacketEncryptionAttribute? Encryption,
        PacketTimeoutAttribute? Timeout
    );

    private static PacketAttributes GetPacketAttributes(MethodInfo method)
    => new(
        method.GetCustomAttribute<PacketIdAttribute>()!,
        method.GetCustomAttribute<PacketPermissionAttribute>(),
        method.GetCustomAttribute<PacketEncryptionAttribute>(),
        method.GetCustomAttribute<PacketTimeoutAttribute>()
    );

    private static async ValueTask DispatchPacketAsync(TPacket packet, IConnection connection)
    {
        packet = TPacket.Compress(packet, connection.ComMode);
        packet = TPacket.Encrypt(packet, connection.EncryptionKey, connection.EncMode);

        await connection.SendAsync(packet);
    }

    /// <summary>
    /// Determines the correct handler based on the method's return type.
    /// </summary>
    private Func<object?, TPacket, IConnection, Task> ResolveHandlerDelegate(Type returnType) => returnType switch
    {
        Type t when t == typeof(void) => (_, _, _) => Task.CompletedTask,
        Type t when t == typeof(byte[]) => async (result, _, connection) =>
        {
            if (result is byte[] data)
                await connection.SendAsync(data);
        }
        ,
        Type t when t == typeof(Memory<byte>) => async (result, _, connection) =>
        {
            if (result is Memory<byte> memory)
                await connection.SendAsync(memory);
        }
        ,
        Type t when t == typeof(TPacket) => async (result, _, connection) =>
        {
            if (result is TPacket packet)
                await PacketDispatcherOptions<TPacket>.DispatchPacketAsync(packet, connection);
        }
        ,
        Type t when t == typeof(ValueTask) => async (result, _, _) =>
        {
            if (result is ValueTask task)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(ValueTask<byte[]>) => async (result, _, connection) =>
        {
            if (result is ValueTask<byte[]> task)
            {
                try
                {
                    byte[] data = await task;
                    await connection.SendAsync(data);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(ValueTask<Memory<byte>>) => async (result, _, connection) =>
        {
            if (result is ValueTask<Memory<byte>> task)
            {
                try
                {
                    Memory<byte> memory = await task;
                    await connection.SendAsync(memory);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(ValueTask<TPacket>) => async (result, _, connection) =>
        {
            if (result is ValueTask<TPacket> task)
            {
                try
                {
                    TPacket packet = await task;
                    await PacketDispatcherOptions<TPacket>.DispatchPacketAsync(packet, connection);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(Task) => async (result, _, _) =>
        {
            if (result is Task task)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(Task<byte[]>) => async (result, _, connection) =>
        {
            if (result is Task<byte[]> task)
            {
                try
                {
                    byte[] data = await task;
                    await connection.SendAsync(data);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(Task<Memory<byte>>) => async (result, _, connection) =>
        {
            if (result is Task<Memory<byte>> task)
            {
                try
                {
                    Memory<byte> memory = await task;
                    await connection.SendAsync(memory);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(Task<TPacket>) => async (result, _, connection) =>
        {
            if (result is Task<TPacket> task)
            {
                try
                {
                    TPacket packet = await task;
                    await PacketDispatcherOptions<TPacket>.DispatchPacketAsync(packet, connection);
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        _ => throw new InvalidOperationException($"Unsupported return type: {returnType}")
    };
}
