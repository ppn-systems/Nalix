using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Threading.Tasks;

namespace Notio.Network.Networking.Handlers;

public sealed partial class PacketDispatcherOptions
{
    private Func<object?, IPacket, IConnection, Task> GetHandler(Type returnType) => returnType switch
    {
        Type t when t == typeof(void) => (_, _, _) => Task.CompletedTask,
        Type t when t == typeof(byte[]) => async (result, _, connection) =>
        {
            if (result is byte[] data)
                await connection.SendAsync(data);
        }
        ,
        Type t when t == typeof(IPacket) => async (result, _, connection) =>
        {
            if (result is IPacket packet)
                await SendPacketAsync(packet, connection);
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
                    Logger?.Error("Error invoking handler", ex);
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
                    Logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(ValueTask<IPacket>) => async (result, _, connection) =>
        {
            if (result is ValueTask<IPacket> task)
            {
                try
                {
                    IPacket packet = await task;
                    await SendPacketAsync(packet, connection);
                }
                catch (Exception ex)
                {
                    Logger?.Error("Error invoking handler", ex);
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
                    Logger?.Error("Error invoking handler", ex);
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
                    Logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        Type t when t == typeof(Task<IPacket>) => async (result, _, connection) =>
        {
            if (result is Task<IPacket> task)
            {
                try
                {
                    IPacket packet = await task;
                    await SendPacketAsync(packet, connection);
                }
                catch (Exception ex)
                {
                    Logger?.Error("Error invoking handler", ex);
                }
            }
        }
        ,
        _ => throw new InvalidOperationException($"Unsupported return type: {returnType}")
    };

    private static T EnsureNotNull<T>(T value, string paramName)
        where T : class => value ?? throw new ArgumentNullException(paramName);

    private async Task SendPacketAsync(IPacket packet, IConnection connection)
    {
        if (SerializationMethod is null)
        {
            Logger?.Error("Serialization method is not set.");
            throw new InvalidOperationException("Serialization method is not set.");
        }

        packet = ProcessPacketFlag(
            "Compression", packet, PacketFlags.IsCompressed, _compressionMethod, connection);

        packet = ProcessPacketFlag(
            "Encryption", packet, PacketFlags.IsEncrypted, _encryptionMethod, connection);

        await connection.SendAsync(SerializationMethod(packet));
    }

    private IPacket ProcessPacketFlag(
        string methodName,
        IPacket packet,
        PacketFlags flag,
        Func<IPacket, IConnection, IPacket>? method,
        IConnection context)
    {
        if (!((packet.Flags & flag) == flag))
            return packet;

        if (method is null)
        {
            Logger?.Error($"{methodName} method is not set, but packet requires {methodName.ToLower()}.");
            return packet;
        }

        return method(packet, context);
    }
}
