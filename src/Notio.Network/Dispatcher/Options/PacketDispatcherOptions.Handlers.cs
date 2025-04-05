using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : class
{
    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketCommandAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. 
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatcherOptions<TPacket> WithHandler<[DynamicallyAccessedMembers(RequiredMembers)] TController>()
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
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="instance"/> is null.
    /// </exception>
    public PacketDispatcherOptions<TPacket> WithHandler<
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
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatcherOptions<TPacket> WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>
        (Func<TController> factory) where TController : class
    {
        Type controllerType = typeof(TController);
        PacketControllerAttribute controllerAttr = controllerType.GetCustomAttribute<PacketControllerAttribute>()
            ?? throw new InvalidOperationException(
                string.Format(Messages.MissingControllerAttribute, controllerType.Name));

        string controllerName = controllerAttr.Name ?? controllerType.Name;

        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        List<MethodInfo> methods = [.. typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketIdAttribute>() != null)];

        if (methods.Count == 0)
        {
            string message = string.Format(Messages.NoMethodsWithPacketCommand, controllerType.Name);

            _logger?.Warn(message);
            throw new InvalidOperationException(message);
        }

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketIdAttribute>()!.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
        {
            string message = string.Format(
                Messages.DuplicateCommandIds, controllerName, string.Join(", ", duplicateCommandIds));

            _logger?.Error(message);
            throw new InvalidOperationException(message);
        }

        List<ushort> registeredCommandIds = [];

        foreach (MethodInfo method in methods)
        {
            ushort commandId = method.GetCustomAttribute<PacketIdAttribute>()!.Id;

            if (PacketHandlers.ContainsKey(commandId))
            {
                string message = string.Format(Messages.CommandIdAlreadyRegistered, commandId);

                _logger?.Error(message);
                throw new InvalidOperationException(message);
            }

            async Task Handler(TPacket packet, IConnection connection)
            {
                Stopwatch? stopwatch = IsMetricsEnabled ? Stopwatch.StartNew() : null;

                if (method.GetCustomAttribute<PacketPermissionAttribute>() is { } accessAttr &&
                    accessAttr.Level > connection.Authority)
                {
                    _logger?.Warn(string.Format(
                        Messages.UnauthorizedCommandAccess,
                        commandId, connection.RemoteEndPoint));
                    return;
                }

                try
                {
                    packet = ProcessPacketFlag(
                        "Compression", packet, PacketFlags.Compressed,
                        _decompressionMethod, connection);

                    packet = ProcessPacketFlag(
                        "Encryption", packet, PacketFlags.Encrypted,
                        _decryptionMethod, connection);

                    object? result = method.Invoke(controllerInstance, [packet, connection]);

                    await GetHandler(method.ReturnType)(result, packet, connection);
                }
                catch (Exception ex)
                {
                    _logger?.Error(string.Format(
                        Messages.CommandHandlerException,
                        controllerName, method.Name, ex.Message));
                    ErrorHandler?.Invoke(ex, commandId);
                }
                finally
                {
                    stopwatch?.Stop();
                    if (stopwatch is not null)
                    {
                        MetricsCallback?.Invoke($"{controllerName}.{method.Name}", stopwatch.ElapsedMilliseconds);
                    }
                }
            }

            PacketHandlers[commandId] = Handler;
            registeredCommandIds.Add(commandId);
        }

        _logger?.Info(string.Format(
            Messages.RegisteredCommandForHandler,
            controllerName,
            string.Join(", ", registeredCommandIds)));

        return this;
    }


    /// <summary>
    /// Attempts to retrieve a registered packet handler for the specified command Number.
    /// </summary>
    /// <param name="commandId">The unique identifier of the packet command.</param>
    /// <param name="handler">
    /// When this method returns, contains the handler function associated with the command Number, 
    /// or <see langword="null"/> if no handler was found.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a handler for the given <paramref name="commandId"/> was found; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method looks up the provided command Number in the internal dictionary of registered packet handlers.
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
    public bool TryGetPacketHandler(ushort commandId, out Func<TPacket, IConnection, Task>? handler)
    {
        if (PacketHandlers.TryGetValue(commandId, out handler))
        {
            Logger?.Debug($"Handler found for Number: {commandId}");
            return true;
        }

        Logger?.Warn($"No handler found for Number: {commandId}");
        return false;
    }

    #region Private Methods

    /// <summary>
    /// Determines the correct handler based on the method's return type.
    /// </summary>
    private Func<object?, TPacket, IConnection, Task> GetHandler(Type returnType) => returnType switch
    {
        Type t when t == typeof(void) => (_, _, _) => Task.CompletedTask,
        Type t when t == typeof(byte[]) => async (result, _, connection) =>
        {
            if (result is byte[] data)
                await connection.SendAsync(data);
        }
        ,
        Type t when t == typeof(TPacket) => async (result, _, connection) =>
        {
            if (result is TPacket packet)
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
        Type t when t == typeof(ValueTask<TPacket>) => async (result, _, connection) =>
        {
            if (result is ValueTask<TPacket> task)
            {
                try
                {
                    TPacket packet = await task;
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
        Type t when t == typeof(Task<TPacket>) => async (result, _, connection) =>
        {
            if (result is Task<TPacket> task)
            {
                try
                {
                    TPacket packet = await task;
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

    private async Task SendPacketAsync(TPacket packet, IConnection connection)
    {
        if (SerializationMethod is null)
        {
            _logger?.Error("Serialization method is not set.");
            throw new InvalidOperationException("Serialization method is not set.");
        }

        packet = ProcessPacketFlag(
            "Compression", packet, PacketFlags.Compressed, _compressionMethod, connection);

        packet = ProcessPacketFlag(
            "Encryption", packet, PacketFlags.Encrypted, _encryptionMethod, connection);

        await connection.SendAsync(SerializationMethod(packet));
    }

    private TPacket ProcessPacketFlag(
        string methodName,
        TPacket packet,
        PacketFlags flag,
        Func<TPacket, IConnection, TPacket>? method,
        IConnection context)
    {
        if (packet is not IPacket ipacket || (ipacket.Flags & flag) != flag)
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
