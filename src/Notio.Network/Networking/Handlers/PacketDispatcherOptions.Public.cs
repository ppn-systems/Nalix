using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Common.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Networking.Handlers;

public sealed partial class PacketDispatcherOptions
{
    /// <summary>
    /// Enables metrics tracking and sets the callback function for reporting execution times.
    /// </summary>
    /// <param name="metricsCallback">The callback function receiving the handler name and execution time in milliseconds.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    public PacketDispatcherOptions WithMetrics(Action<string, long> metricsCallback)
    {
        EnableMetrics = true;
        MetricsCallback = metricsCallback;
        Logger?.Debug("Metrics tracking enabled.");
        return this;
    }

    /// <summary>
    /// Configures logging for the packet dispatcher.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    public PacketDispatcherOptions WithLogging(ILogger logger)
    {
        Logger = logger;
        Logger.Debug("Logging configured.");
        return this;
    }

    /// <summary>
    /// Configures a custom error handler for exceptions occurring during packet processing.
    /// </summary>
    /// <param name="errorHandler">The error handler action.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    public PacketDispatcherOptions WithErrorHandler(Action<Exception, ushort> errorHandler)
    {
        ErrorHandler = errorHandler;
        Logger?.Debug("Error handler configured.");
        return this;
    }

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
                    Logger?.Warn(
                        $"Unauthorized access to CommandId: {commandId} from {connection.RemoteEndPoint}");
                    return;
                }

                try
                {
                    if (EnableMetrics)
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

        Logger?.Info(
            $"Registered {typeof(TController).Name} for CommandIds: {string.Join(", ", registeredCommandIds)}");

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet compression method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for compression.</typeparam>
    /// <param name="compressionMethod">
    /// A function that compresses a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedCompression<TPacket>(
        Func<TPacket, IConnection, TPacket> compressionMethod)
        where TPacket : IPacket
    {
        if (compressionMethod is not null)
        {
            _compressionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? compressionMethod(typedPacket, connection)
                    : packet;

            Logger?.Debug($"Type-specific packet compression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet decompression method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for decompression.</typeparam>
    /// <param name="decompressionMethod">
    /// A function that decompresses a packet of type <typeparamref name="TPacket"/> before processing.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedDecompression<TPacket>(
        Func<TPacket, IConnection, TPacket> decompressionMethod)
        where TPacket : IPacket
    {
        if (decompressionMethod is not null)
        {
            _decompressionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? decompressionMethod(typedPacket, connection)
                    : packet;

            Logger?.Debug($"Type-specific packet decompression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet encryption method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for encryption.</typeparam>
    /// <param name="encryptionMethod">
    /// A function that encrypts a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedEncryption<TPacket>(
        Func<TPacket, IConnection, TPacket> encryptionMethod)
        where TPacket : IPacket
    {
        if (encryptionMethod is not null)
        {
            _encryptionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? encryptionMethod(typedPacket, connection)
                    : packet;

            Logger?.Debug($"Type-specific packet encryption configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet decryption method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for decryption.</typeparam>
    /// <param name="decryptionMethod">
    /// A function that decrypts a packet of type <typeparamref name="TPacket"/> before processing.
    /// The function receives the packet and connection context, and returns the decrypted packet.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedDecryption<TPacket>(
        Func<TPacket, IConnection, TPacket> decryptionMethod)
        where TPacket : IPacket
    {
        if (decryptionMethod is not null)
        {
            _decryptionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? decryptionMethod(typedPacket, connection)
                    : packet;

            Logger?.Debug($"Type-specific packet decryption configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet serialization method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for serialization.</typeparam>
    /// <param name="serializer">A strongly-typed function that serializes a packet of type <typeparamref name="TPacket"/> into a <see cref="Memory{Byte}"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedSerializer<TPacket>(
        Func<TPacket, Memory<byte>> serializer)
        where TPacket : IPacket
    {
        ArgumentNullException.ThrowIfNull(serializer);

        // Create adapter function - check if packet is TPacket before calling serializer
        SerializationMethod = packet =>
        {
            if (packet is TPacket typedPacket) return serializer(typedPacket);

            throw new InvalidOperationException(
                $"Cannot serialize packet of type {packet.GetType().Name}. Expected {typeof(TPacket).Name}.");
        };

        Logger?.Debug($"Type-specific packet serialization configured for {typeof(TPacket).Name}.");
        return this;
    }

    /// <summary>
    /// Configures a type-specific packet deserialization method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for deserialization.</typeparam>
    /// <param name="deserializer">A strongly-typed function that deserializes a <see cref="ReadOnlyMemory{Byte}"/> into a packet of type <typeparamref name="TPacket"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if deserializer is null.</exception>
    /// <remarks>
    /// This method provides type safety by ensuring the deserialization process returns the expected packet type.
    /// </remarks>
    public PacketDispatcherOptions WithTypedDeserializer<TPacket>(
        Func<ReadOnlyMemory<byte>, TPacket> deserializer)
        where TPacket : IPacket
    {
        ArgumentNullException.ThrowIfNull(deserializer);

        DeserializationMethod = bytes => deserializer(bytes);

        Logger?.Debug($"Type-specific packet deserialization configured for {typeof(TPacket).Name}.");
        return this;
    }
}
