using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Network.Core.Packets;
using Notio.Network.Dispatcher.BuiltIn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket>
    where TPacket : IPacket, IPacketCompressor<TPacket>, IPacketEncryptor<TPacket>
{
    /// <summary>
    /// Registers default handlers for the packet dispatcher, including controllers for session management,
    /// keep-alive functionality, and mode handling. This method sets up the standard set of handlers 
    /// used by the dispatcher using their respective controller types.
    /// </summary>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance, allowing for method chaining.
    /// </returns>
    public PacketDispatcherOptions<TPacket> WithHandlerDefault()
    {
        this.WithHandler<SessionController>();
        this.WithHandler<KeepAliveController>();
        this.WithHandler(() => new ModeController(_logger));

        return this;
    }

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
            ?? throw new InvalidOperationException($"SessionController '{controllerType.Name}' missing PacketController attribute.");

        string controllerName = controllerAttr.Name ?? controllerType.Name;

        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        // Log method scanning process
        _logger?.Debug("Scanning '{0}' for packet handler methods...", controllerName);

        List<MethodInfo> methods = [.. typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketIdAttribute>() != null)];

        if (methods.Count == 0)
        {
            string message = $"SessionController '{controllerType.FullName}' has no methods marked with [PacketId]. " +
                             $"Ensure at least one public method is decorated.";

            _logger?.Warn(message);
            throw new InvalidOperationException(message);
        }

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketIdAttribute>()!.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
        {
            string message = $"Duplicate PacketId values found in controller " +
                             $"'{controllerName}': {string.Join(", ", duplicateCommandIds)}. " +
                             $"Each handler must have a unique ID.";

            _logger?.Error(message);
            throw new InvalidOperationException(message);
        }

        List<ushort> registeredIds = [];

        foreach (MethodInfo method in methods)
        {
            ushort id = method.GetCustomAttribute<PacketIdAttribute>()!.Id;

            if (PacketHandlers.ContainsKey(id))
            {
                _logger?.Error("PacketId '{0}' already registered in another controller. Conflict in controller '{1}'.",
                                id, controllerName);

                throw new InvalidOperationException($"PacketId '{id}' already registered.");
            }

            PacketHandlers[id] = this.CreateHandlerDelegate(method, controllerInstance);
            registeredIds.Add(id);
        }

        _logger?.Info("Registered {0} packet handlers in controller '{1}': [{2}]",
                       registeredIds.Count, controllerName, string.Join(", ", registeredIds));

        return this;
    }

    /// <summary>
    /// Attempts to retrieve a registered packet handler for the specified command Number.
    /// </summary>
    /// <param name="id">The unique identifier of the packet command.</param>
    /// <param name="handler">
    /// When this method returns, contains the handler function associated with the command Number, 
    /// or <see langword="null"/> if no handler was found.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a handler for the given <paramref name="id"/> was found; 
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
    public bool TryResolveHandler(ushort id, out Func<TPacket, IConnection, Task>? handler)
    {
        if (PacketHandlers.TryGetValue(id, out handler))
            return true;

        Logger?.Warn("No handler found for packet [ID={0}]", id);
        return false;
    }

    private Func<TPacket, IConnection, Task> CreateHandlerDelegate(MethodInfo method, object controllerInstance)
    {
        PacketAttributes attributes = PacketDispatcherOptions<TPacket>.GetPacketAttributes(method);

        return async (packet, connection) =>
        {
            Stopwatch? stopwatch = IsMetricsEnabled ? Stopwatch.StartNew() : null;

            if (!this.CheckRateLimit(connection.RemoteEndPoint, attributes, method))
            {
                _logger?.Warn("Rate limit exceeded on '{0}' from {1}", method.Name, connection.RemoteEndPoint);

                connection.SendCode(PacketCode.RateLimited);
                return;
            }

            if (attributes.Permission?.Level > connection.Level)
            {
                _logger?.Warn("You do not have permission to perform this action.");
                connection.SendCode(PacketCode.PermissionDenied);
                return;
            }

            // Handle Compression (e.g., apply compression to packet)
            try { packet = TPacket.Decompress(packet, connection.ComMode); }
            catch (Exception ex)
            {
                _logger?.Error("Failed to decompress packet: {0}", ex.Message);
                connection.SendCode(PacketCode.ServerError);
                return;
            }

            if (attributes.Encryption?.IsEncrypted == true && !packet.IsEncrypted)
            {
                string message = $"Encrypted packet not allowed for command " +
                                 $"'{attributes.PacketId.Id}' " +
                                 $"from connection {connection.RemoteEndPoint}.";

                _logger?.Warn(message);
                connection.SendCode(PacketCode.PacketEncryption);
                return;
            }
            else
            {
                // Handle Encryption (e.g., apply encryption to packet)
                packet = TPacket.Decrypt(packet, connection.EncryptionKey, connection.EncMode);
            }

            try
            {
                object? result;

                // Cache method invocation with improved performance
                if (attributes.Timeout != null)
                {
                    using CancellationTokenSource cts = new(attributes.Timeout.TimeoutMilliseconds);
                    try
                    {
                        result = await Task.Run(() => method.Invoke(controllerInstance, [packet, connection]), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.Error("Packet '{0}' timed out after {1}ms.",
                            attributes.PacketId.Id,
                            attributes.Timeout.TimeoutMilliseconds);
                        connection.SendCode(PacketCode.RequestTimeout);

                        return;
                    }
                }
                else
                {
                    result = method.Invoke(controllerInstance, [packet, connection]);
                }

                // Await the return result, could be ValueTask if method is synchronous
                await ResolveHandlerDelegate(method.ReturnType)(result, packet, connection).ConfigureAwait(false);
            }
            catch (PackageException ex)
            {
                _logger?.Error("Error occurred while processing packet id '{0}' in controller '{1}' (Method: '{2}'). " +
                               "Exception: {3}. Remote: {4}, Exception Details: {5}",
                    attributes.PacketId.Id,           // Command ID
                    controllerInstance.GetType().Name,// SessionController name
                    method.Name,                      // Method name
                    ex.GetType().Name,                // Exception type
                    connection.RemoteEndPoint,        // Connection details for traceability
                    ex.Message                        // Exception message itself
                );
                ErrorHandler?.Invoke(ex, attributes.PacketId.Id);
                connection.SendCode(PacketCode.ServerError);
            }
            catch (Exception ex)
            {
                _logger?.Error("Packet [Id={0}] ({1}.{2}) threw {3}: {4} [Remote: {5}]",
                    attributes.PacketId.Id,
                    controllerInstance.GetType().Name,
                    method.Name,
                    ex.GetType().Name,
                    ex.Message,
                    connection.RemoteEndPoint
                );
                ErrorHandler?.Invoke(ex, attributes.PacketId.Id);
                connection.SendCode(PacketCode.ServerError);
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    MetricsCallback?.Invoke($"{controllerInstance.GetType().Name}.{method.Name}", stopwatch.ElapsedMilliseconds);
                }
            }
        };
    }
}
