using Nalix.Common.Connection;
using Nalix.Common.Package;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private System.Func<object?, TPacket, IConnection, Task> ResolveHandlerDelegate(System.Type returnType)
        => _handlerLookup.TryGetValue(returnType, out var handler)
            ? handler : CreateUnsupportedHandler(returnType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private System.Func<object?, TPacket, IConnection, Task> CreateUnsupportedHandler(System.Type returnType)
        => (_, _, _) =>
        {
            _logger?.Warn("Unsupported return type: {0}", returnType.Name);
            return Task.CompletedTask;
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private System.Collections.Frozen.FrozenDictionary<
        System.Type, System.Func<System.Object?, TPacket, IConnection, Task>> CreateHandlerLookup()
    {
        System.Collections.Generic.Dictionary<
            System.Type, System.Func<
                System.Object?, TPacket, IConnection, Task>> handlers = new()
                {
                    [typeof(void)] = static (_, _, _) => Task.CompletedTask,
                    [typeof(byte[])] = CreateByteArrayHandler(),
                    [typeof(string)] = CreateStringHandler(),
                    [typeof(System.Memory<byte>)] = CreateMemoryHandler(),
                    [typeof(TPacket)] = CreatePacketHandler(),
                    [typeof(ValueTask)] = CreateValueTaskHandler(),
                    [typeof(ValueTask<byte[]>)] = CreateValueTaskByteArrayHandler(),
                    [typeof(ValueTask<string>)] = CreateValueTaskStringHandler(),
                    [typeof(ValueTask<System.Memory<byte>>)] = CreateValueTaskMemoryHandler(),
                    [typeof(ValueTask<TPacket>)] = CreateValueTaskPacketHandler(),
                    [typeof(Task)] = CreateTaskHandler(),
                    [typeof(Task<byte[]>)] = CreateTaskByteArrayHandler(),
                    [typeof(Task<string>)] = CreateTaskStringHandler(),
                    [typeof(Task<System.Memory<byte>>)] = CreateTaskMemoryHandler(),
                    [typeof(Task<TPacket>)] = CreateTaskPacketHandler(),
                };

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(handlers);
    }

    #region Factory methods

    private static System.Func<object?, TPacket, IConnection, Task> CreateByteArrayHandler()
        => static async (result, _, connection) =>
        {
            if (result is byte[] data)
                await connection.Tcp.SendAsync(data);
        };

    private static System.Func<object?, TPacket, IConnection, Task> CreateStringHandler()
        => static async (result, _, connection) =>
        {
            if (result is string data)
                await connection.Tcp.SendAsync(TPacket.Create(0, data));
        };

    private static System.Func<object?, TPacket, IConnection, Task> CreateMemoryHandler()
        => static async (result, _, connection) =>
        {
            if (result is System.Memory<byte> memory)
                await connection.Tcp.SendAsync(memory);
        };

    private static System.Func<object?, TPacket, IConnection, Task> CreatePacketHandler()
        => static async (result, _, connection) =>
        {
            if (result is TPacket packet)
                await DispatchPacketAsync(packet, connection);
        };

    // ValueTask handlers
    private System.Func<object?, TPacket, IConnection, Task> CreateValueTaskHandler()
        => async (result, _, _) =>
        {
            if (result is ValueTask task)
            {
                try { await task; }
                catch (System.Exception ex) { Failure(typeof(ValueTask), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateValueTaskByteArrayHandler()
        => async (result, _, connection) =>
        {
            if (result is ValueTask<byte[]> task)
            {
                try
                {
                    var data = await task;
                    await connection.Tcp.SendAsync(data);
                }
                catch (System.Exception ex) { Failure(typeof(ValueTask<byte[]>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateValueTaskStringHandler()
        => async (result, _, connection) =>
        {
            if (result is ValueTask<string> task)
            {
                try
                {
                    var data = await task;
                    using var packet = TPacket.Create(0, data);
                    await connection.Tcp.SendAsync(packet.Serialize());
                }
                catch (System.Exception ex) { Failure(typeof(ValueTask<string>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateValueTaskMemoryHandler()
        => async (result, _, connection) =>
        {
            if (result is ValueTask<System.Memory<byte>> task)
            {
                try
                {
                    var memory = await task;
                    await connection.Tcp.SendAsync(memory);
                }
                catch (System.Exception ex) { Failure(typeof(ValueTask<System.Memory<byte>>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateValueTaskPacketHandler()
        => async (result, _, connection) =>
        {
            if (result is ValueTask<TPacket> task)
            {
                try
                {
                    var packet = await task;
                    await DispatchPacketAsync(packet, connection);
                }
                catch (System.Exception ex) { Failure(typeof(ValueTask<TPacket>), ex); }
            }
        };

    // Task handlers
    private System.Func<object?, TPacket, IConnection, Task> CreateTaskHandler()
        => async (result, _, _) =>
        {
            if (result is Task task)
            {
                try { await task; }
                catch (System.Exception ex) { Failure(typeof(Task), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateTaskByteArrayHandler()
        => async (result, _, connection) =>
        {
            if (result is Task<byte[]> task)
            {
                try
                {
                    var data = await task;
                    await connection.Tcp.SendAsync(data);
                }
                catch (System.Exception ex) { Failure(typeof(Task<byte[]>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateTaskStringHandler()
        => async (result, _, connection) =>
        {
            if (result is Task<string> task)
            {
                try
                {
                    var data = await task;
                    using var packet = TPacket.Create(0, data);
                    await connection.Tcp.SendAsync(packet.Serialize());
                }
                catch (System.Exception ex) { Failure(typeof(Task<string>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateTaskMemoryHandler()
        => async (result, _, connection) =>
        {
            if (result is Task<System.Memory<byte>> task)
            {
                try
                {
                    var memory = await task;
                    await connection.Tcp.SendAsync(memory);
                }
                catch (System.Exception ex) { Failure(typeof(Task<System.Memory<byte>>), ex); }
            }
        };

    private System.Func<object?, TPacket, IConnection, Task> CreateTaskPacketHandler()
        => async (result, _, connection) =>
        {
            if (result is Task<TPacket> task)
            {
                try
                {
                    var packet = await task;
                    await DispatchPacketAsync(packet, connection);
                }
                catch (System.Exception ex) { Failure(typeof(Task<TPacket>), ex); }
            }
        };

    #endregion Factory methods
}