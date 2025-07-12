using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Handlers;

/// <summary>
/// Factory để tạo return type handlers với zero allocation approach.
/// </summary>
/// <typeparam name="TPacket">Packet type</typeparam>
public static class ReturnTypeHandlerFactory<TPacket>
    where TPacket : Common.Package.IPacket,
                   Common.Package.IPacketFactory<TPacket>,
                   Common.Package.IPacketEncryptor<TPacket>,
                   Common.Package.IPacketCompressor<TPacket>
{
    /// <summary>
    /// Cached handlers để tránh recreation.
    /// </summary>
    private static readonly FrozenDictionary<System.Type, IReturnTypeHandler<TPacket>> _handlers = CreateHandlers();

    /// <summary>
    /// Get handler cho specific return type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReturnTypeHandler<TPacket> GetHandler(System.Type returnType)
    {
        if (_handlers.TryGetValue(returnType, out var handler))
            return handler;

        // Handle generic Task<T> và ValueTask<T>
        if (returnType.IsGenericType)
        {
            var genericType = returnType.GetGenericTypeDefinition();
            var genericArg = returnType.GetGenericArguments()[0];

            if (genericType == typeof(System.Threading.Tasks.Task<>))
            {
                var innerHandler = GetHandler(genericArg);
                return CreateTaskHandler(innerHandler, genericArg);
            }

            if (genericType == typeof(System.Threading.Tasks.ValueTask<>))
            {
                var innerHandler = GetHandler(genericArg);
                return CreateValueTaskHandler(innerHandler, genericArg);
            }
        }

        // Fallback cho unsupported types
        return new UnsupportedReturnHandler<TPacket>(returnType);
    }

    /// <summary>
    /// Create base handlers dictionary.
    /// </summary>
    private static FrozenDictionary<System.Type, IReturnTypeHandler<TPacket>> CreateHandlers()
    {
        var handlers = new System.Collections.Generic.Dictionary<System.Type, IReturnTypeHandler<TPacket>>
        {
            [typeof(void)] = new VoidReturnHandler<TPacket>(),
            [typeof(TPacket)] = new PacketReturnHandler<TPacket>(),
            [typeof(System.Byte[])] = new ByteArrayReturnHandler<TPacket>(),
            [typeof(System.String)] = new StringReturnHandler<TPacket>(),
            [typeof(System.Memory<System.Byte>)] = new MemoryReturnHandler<TPacket>(),
            [typeof(System.ReadOnlyMemory<System.Byte>)] = new ReadOnlyMemoryReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.Task)] = new TaskVoidReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.ValueTask)] = new ValueTaskVoidReturnHandler<TPacket>(),
        };

        return handlers.ToFrozenDictionary();
    }

    private static IReturnTypeHandler<TPacket> CreateTaskHandler(
        IReturnTypeHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        var handlerType = typeof(TaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnTypeHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }

    private static IReturnTypeHandler<TPacket> CreateValueTaskHandler(
        IReturnTypeHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        var handlerType = typeof(ValueTaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnTypeHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }
}