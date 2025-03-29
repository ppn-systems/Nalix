using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Common.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.PacketProcessing.Options;

public sealed partial class PacketDispatcherOptions
{
    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketCommandAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. 
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatcherOptions WithHandler<[DynamicallyAccessedMembers(RequiredMembers)] TController>()
        where TController : class, new()
        => WithHandler(() => new TController());

    /// <summary>
    /// Registers a handler using an existing instance of the specified controller type.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// </typeparam>
    /// <param name="instance">
    /// An existing instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions"/> instance for chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="instance"/> is null.
    /// </exception>
    public PacketDispatcherOptions WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>
        (TController instance) where TController : class
        => WithHandler(() => EnsureNotNull(instance, nameof(instance)));

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// using a provided factory function, then scanning its methods decorated 
    /// with <see cref="PacketCommandAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. This type does not require 
    /// a parameterless constructor.
    /// </typeparam>
    /// <param name="factory">
    /// A function that returns an instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatcherOptions WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>
        (Func<TController> factory) where TController : class
    {
        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        // Get methods from the controller that are decorated with PacketCommandAttribute
        List<MethodInfo> methods = [.. typeof(TController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Where(m => m.GetCustomAttribute<PacketCommandAttribute>() != null)];

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"No methods found with [PacketCommand] in {typeof(TController).Name}.");
        }

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketCommandAttribute>()!.CommandId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
            throw new InvalidOperationException(
                $"Duplicate CommandIds found in {typeof(TController).Name}: " +
                $"{string.Join(", ", duplicateCommandIds)}");

        List<ushort> registeredCommandIds = [];

        // Register each method with its corresponding commandId
        foreach (MethodInfo method in methods)
        {
            Type returnType = method.ReturnType;
            ushort commandId = method.GetCustomAttribute<PacketCommandAttribute>()!.CommandId;

            PacketAccessAttribute? accessAttr = method.GetCustomAttribute<PacketAccessAttribute>();
            bool hasAccessLevel = accessAttr != null;
            AccessLevel level = hasAccessLevel && accessAttr != null ? accessAttr.Level : default;

            Func<object?, IPacket, IConnection, Task> value = GetHandler(returnType);

            if (PacketHandlers.ContainsKey(commandId))
            {
                throw new InvalidOperationException(
                    $"CommandId {commandId} is already registered by another handler.");
            }

            async Task Handler(IPacket packet, IConnection connection)
            {
                Stopwatch? stopwatch = null;

                if (hasAccessLevel && level > connection.Authority)
                {
                    _logger?.Warn(
                        $"Unauthorized access to CommandId: {commandId} from {connection.RemoteEndPoint}");
                    return;
                }

                try
                {
                    if (IsMetricsEnabled)
                    {
                        stopwatch = Stopwatch.StartNew();
                    }

                    TController controller = controllerInstance;
                    object? result;

                    try
                    {
                        packet = ProcessPacketFlag("Compression", packet,
                            PacketFlags.IsCompressed, _decompressionMethod, connection);

                        packet = ProcessPacketFlag("Encryption", packet,
                            PacketFlags.IsEncrypted, _decryptionMethod, connection);

                        result = method.Invoke(controller, [packet, connection]);
                    }
                    catch (Exception ex)
                    {
                        throw new InternalErrorException($"Error invoking handler for CommandId: {commandId}", ex);
                    }

                    await value(result, packet, connection);
                }
                catch (Exception ex)
                {
                    if (ErrorHandler is not null) ErrorHandler(ex, commandId);
                    else _logger?.Error($"Unhandled exception in CommandId {commandId}", ex);
                }
                finally
                {
                    if (stopwatch != null)
                    {
                        stopwatch.Stop();
                        MetricsCallback?.Invoke(
                            $"{typeof(TController).Name}.{method.Name}", stopwatch.ElapsedMilliseconds);
                    }
                }
            }

            PacketHandlers[commandId] = Handler;
            registeredCommandIds.Add(commandId);
        }

        _logger?.Info(
            $"Registered {typeof(TController).Name} for CommandIds: {string.Join(", ", registeredCommandIds)}");

        return this;
    }

    /// <summary>
    /// Attempts to retrieve a registered packet handler for the specified command ID.
    /// </summary>
    /// <param name="commandId">The unique identifier of the packet command.</param>
    /// <param name="handler">
    /// When this method returns, contains the handler function associated with the command ID, 
    /// or <see langword="null"/> if no handler was found.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a handler for the given <paramref name="commandId"/> was found; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method looks up the provided command ID in the internal dictionary of registered packet handlers.
    /// If a matching handler is found, it is returned via the <paramref name="handler"/> output parameter.
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to retrieve and invoke a packet handler:
    /// <code>
    /// if (options.TryGetPacketHandler(commandId, out var handler))
    /// {
    ///     await handler(packet, connection);
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No handler found.");
    /// }
    /// </code>
    /// </example>
    public bool TryGetPacketHandler(ushort commandId, out Func<IPacket, IConnection, Task>? handler)
    {
        if (PacketHandlers.TryGetValue(commandId, out handler))
        {
            Logger?.Debug($"Handler found for CommandId: {commandId}");
            return true;
        }

        Logger?.Warn($"No handler found for CommandId: {commandId}");
        return false;
    }

    #region Private Methods

    /// <summary>
    /// Determines the correct handler based on the method's return type.
    /// </summary>
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
                    _logger?.Error("Error invoking handler", ex);
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
            _logger?.Error("Serialization method is not set.");
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
            _logger?.Error($"{methodName} method is not set, but packet requires {methodName.ToLower()}.");
            return packet;
        }

        return method(packet, context);
    }

    #endregion
}
