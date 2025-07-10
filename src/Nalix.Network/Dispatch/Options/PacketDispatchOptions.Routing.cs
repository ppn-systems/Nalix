using Nalix.Common.Connection;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        ResolveHandlerDelegate(System.Type returnType)
        => _typeCache.TryGetValue(
            returnType, out System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>? handler)
            ? handler : CreateUnsupportedHandler(returnType);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateUnsupportedHandler(System.Type returnType)
        => (_, _, _) =>
        {
            _logger?.Warn("Unsupported return type: {0}", returnType.Name);
            return System.Threading.Tasks.Task.CompletedTask;
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Collections.Frozen.FrozenDictionary<
        System.Type, System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>>
        CreateHandlerLookup()
    {
        System.Collections.Generic.Dictionary<
            System.Type, System.Func<
                System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>> handlers = new()
                {
                    [typeof(void)] = static (_, _, _) => System.Threading.Tasks.Task.CompletedTask,
                    [typeof(TPacket)] = CreatePacketHandler(),
                    [typeof(System.Byte[])] = CreateByteArrayHandler(),
                    [typeof(System.String)] = CreateStringHandler(),
                    [typeof(System.Memory<System.Byte>)] = CreateMemoryHandler(),
                    [typeof(System.ReadOnlyMemory<System.Byte>)] = CreateReadOnlyMemoryHandler(),
                    [typeof(System.Collections.Generic.IEnumerable<TPacket>)] = CreatePacketEnumerableHandler(),
                    [typeof(System.Threading.Tasks.ValueTask)] = CreateValueTaskHandler(),
                    [typeof(System.Threading.Tasks.ValueTask<TPacket>)] = CreateValueTaskPacketHandler(),
                    [typeof(System.Threading.Tasks.ValueTask<System.Byte[]>)] = CreateValueTaskByteArrayHandler(),
                    [typeof(System.Threading.Tasks.ValueTask<System.String>)] = CreateValueTaskStringHandler(),
                    [typeof(System.Threading.Tasks.ValueTask<System.Memory<System.Byte>>)] = CreateValueTaskMemoryHandler(),
                    [typeof(System.Threading.Tasks.Task)] = CreateTaskHandler(),
                    [typeof(System.Threading.Tasks.Task<TPacket>)] = CreateTaskPacketHandler(),
                    [typeof(System.Threading.Tasks.Task<System.Byte[]>)] = CreateTaskByteArrayHandler(),
                    [typeof(System.Threading.Tasks.Task<System.String>)] = CreateTaskStringHandler(),
                    [typeof(System.Threading.Tasks.Task<System.Memory<System.Byte>>)] = CreateTaskMemoryHandler(),
                };

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(handlers);
    }

    #region Factory methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateByteArrayHandler()
        => static async (result, _, connection) =>
        {
            if (result is System.Byte[] data)
                await connection.Tcp.SendAsync(data);
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateStringHandler()
        => static async (result, _, connection) =>
        {
            if (result is System.String data)
                await connection.Tcp.SendAsync(TPacket.Create(0, data));
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateMemoryHandler()
        => static async (result, _, connection) =>
        {
            if (result is System.Memory<System.Byte> memory)
                await connection.Tcp.SendAsync(memory);
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateReadOnlyMemoryHandler()
        => static async (result, _, connection) =>
        {
            if (result is System.ReadOnlyMemory<System.Byte> memory)
                await connection.Tcp.SendAsync(memory);
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreatePacketHandler()
        => static async (result, _, connection) =>
        {
            if (result is TPacket packet)
                await DispatchPacketAsync(packet, connection);
        };

    private static System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreatePacketEnumerableHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Collections.Generic.IEnumerable<TPacket> packets)
            {
                foreach (var packet in packets)
                    await DispatchPacketAsync(packet, connection);
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateValueTaskHandler()
        => async (result, _, _) =>
        {
            if (result is System.Threading.Tasks.ValueTask task)
            {
                try { await task; }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.ValueTask), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateValueTaskByteArrayHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.ValueTask<System.Byte[]> task)
            {
                try
                {
                    var data = await task;
                    await connection.Tcp.SendAsync(data);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.ValueTask<System.Byte[]>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateValueTaskStringHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.ValueTask<System.String> task)
            {
                try
                {
                    System.String data = await task;
                    using TPacket packet = TPacket.Create(0, data);
                    await connection.Tcp.SendAsync(packet.Serialize());
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.ValueTask<System.String>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateValueTaskMemoryHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.ValueTask<System.Memory<System.Byte>> task)
            {
                try
                {
                    var memory = await task;
                    await connection.Tcp.SendAsync(memory);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.ValueTask<System.Memory<System.Byte>>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateValueTaskPacketHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.ValueTask<TPacket> task)
            {
                try
                {
                    TPacket packet = await task;
                    await DispatchPacketAsync(packet, connection);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.ValueTask<TPacket>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateTaskHandler()
        => async (result, _, _) =>
        {
            if (result is System.Threading.Tasks.Task task)
            {
                try { await task; }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.Task), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateTaskByteArrayHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.Task<System.Byte[]> task)
            {
                try
                {
                    System.Byte[] data = await task;
                    await connection.Tcp.SendAsync(data);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.Task<System.Byte[]>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateTaskStringHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.Task<System.String> task)
            {
                try
                {
                    System.String data = await task;
                    using TPacket packet = TPacket.Create(0, data);
                    await connection.Tcp.SendAsync(packet.Serialize());
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.Task<System.String>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateTaskMemoryHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.Task<System.Memory<System.Byte>> task)
            {
                try
                {
                    var memory = await task;
                    await connection.Tcp.SendAsync(memory);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.Task<System.Memory<System.Byte>>), ex); }
            }
        };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>
        CreateTaskPacketHandler()
        => async (result, _, connection) =>
        {
            if (result is System.Threading.Tasks.Task<TPacket> task)
            {
                try
                {
                    var packet = await task;
                    await DispatchPacketAsync(packet, connection);
                }
                catch (System.Exception ex) { Failure(typeof(System.Threading.Tasks.Task<TPacket>), ex); }
            }
        };

    #endregion Factory methods
}