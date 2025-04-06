using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Network.Core.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketIdAttribute"/>.
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
    /// with <see cref="PacketIdAttribute"/>.
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
            ?? throw new InvalidOperationException($"Controller '{controllerType.Name}' missing PacketController attribute.");

        string controllerName = controllerAttr.Name ?? controllerType.Name;

        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        // Log method scanning process
        _logger?.Debug($"Scanning methods in controller '{controllerName}' for packet handlers...");

        List<MethodInfo> methods = [.. typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketIdAttribute>() != null)];

        if (methods.Count == 0)
        {
            string message = $"No methods found with PacketId attribute in controller '{controllerType.Name}'.";
            _logger?.Warn(message);
            throw new InvalidOperationException(message);
        }

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketIdAttribute>()!.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
        {
            string message = $"Duplicate command IDs detected in controller " +
                             $"'{controllerName}': {string.Join(", ", duplicateCommandIds)}";
            _logger?.Error(message);
            throw new InvalidOperationException(message);
        }

        List<ushort> registeredIds = [];

        foreach (MethodInfo method in methods)
        {
            ushort id = method.GetCustomAttribute<PacketIdAttribute>()!.Id;

            if (PacketHandlers.ContainsKey(id))
            {
                string message = $"ID '{id}' already registered for handler.";
                _logger?.Error(message);
                throw new InvalidOperationException(message);
            }

            async Task Handler(TPacket packet, IConnection connection)
            {
                Stopwatch? stopwatch = IsMetricsEnabled ? Stopwatch.StartNew() : null;

                if (method.GetCustomAttribute<PacketPermissionAttribute>() is { } accessAttr &&
                    accessAttr.Level > connection.Level)
                {
                    _logger?.Warn("You do not have permission to perform this action.");

                    connection.SendString(PacketCode.PermissionDenied);
                    return;
                }

                if (method.GetCustomAttribute<PacketEncryptionAttribute>() is { IsEncrypted: true })
                {
                    if (!packet.IsEncrypted)
                    {
                        string message = $"Encrypted packet not allowed for command '{id}' " +
                                         $"from connection {connection.RemoteEndPoint}.";

                        _logger?.Error(message);

                        connection.SendString(PacketCode.PacketEncryption);
                        return;
                    }
                }

                try
                {
                    packet = ApplyCompression(packet, connection);
                    packet = ApplyEncryption(packet, connection);

                    object? result = method.Invoke(controllerInstance, [packet, connection]);

                    await ResolveHandlerDelegate(method.ReturnType)(result, packet, connection);
                }
                catch (PackageException ex)
                {
                    // Enhanced error log with context on the exception
                    string message = string.Format(
                        "Error occurred while processing command '{0}' in controller '{1}' (Method: '{2}'). " +
                        "Exception: {3}. Packet info: Command ID: {4}, RemoteEndPoint: {5}, Exception Details: {6}",
                        id,                         // Command ID
                        controllerName,             // Controller name
                        method.Name,                // Method name that caused the error
                        ex.GetType().Name,          // Exception type
                        id,                         // Command ID for context
                        connection.RemoteEndPoint,  // Connection details for traceability
                        ex.Message                  // Exception message itself
                    );

                    _logger?.Error(message);        // Log the detailed error message
                    ErrorHandler?.Invoke(ex, id);   // Invoke custom error handler if set

                    connection.SendString(PacketCode.ServerError);
                }
                catch (Exception ex)
                {
                    string message = $"General error while processing command '{id}' " +
                                     $"in controller '{controllerName}' (Method: '{method.Name}')." +
                                     $"Exception: {ex.Message}, " +
                                     $"Packet info: Command ID: {id}, " +
                                     $"Remote Endpoint: {connection.RemoteEndPoint}";

                    _logger?.Error(message);
                    ErrorHandler?.Invoke(ex, id);

                    connection.SendString(PacketCode.ServerError);
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

            PacketHandlers[id] = Handler;
            registeredIds.Add(id);
        }

        _logger?.Info($"Successfully registered handlers for command IDs: " +
                      $"{string.Join(", ", registeredIds)} in controller '{controllerName}'.");

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
    /// if (options.TryResolveHandler(id, out var handler))
    /// {
    ///     await handler(packet, connection);
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No handler found.");
    /// }
    /// </code>
    /// </example>
    public bool TryResolveHandler(ushort commandId, out Func<TPacket, IConnection, Task>? handler)
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
                await DispatchPacketAsync(packet, connection);
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
                    await DispatchPacketAsync(packet, connection);
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
                    await DispatchPacketAsync(packet, connection);
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

    private async Task DispatchPacketAsync(TPacket packet, IConnection connection)
    {
        packet = ApplyCompression(packet, connection);
        packet = ApplyEncryption(packet, connection);

        await connection.SendAsync(packet.Serialize());
    }

    private TPacket ApplyCompression(TPacket packet, IConnection connection)
    {
        if (_pCompressionMethod is null)
        {
            _logger?.Error("Compression method is not set, but packet requires compression.");
            return packet;
        }

        return _pCompressionMethod(packet, connection);
    }

    private TPacket ApplyEncryption(TPacket packet, IConnection connection)
    {
        if (_pEncryptionMethod is null)
        {
            _logger?.Error("Encryption method is not set, but packet requires encryption.");
            return packet;
        }

        return _pEncryptionMethod(packet, connection);
    }

    #endregion
}
