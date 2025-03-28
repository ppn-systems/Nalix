using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Common.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Networking.Handlers;

/// <summary>
/// Provides configuration options for an instance of <see cref="PacketDispatcher"/>.
/// </summary>
/// <remarks>
/// This class allows registering packet handlers, configuring logging, and defining error-handling strategies.
/// </remarks>
public sealed class PacketDispatcherOptions
{
    #region Fields

    private Func<IPacket, IConnection, IPacket>? _encryptionMethod;
    private Func<IPacket, IConnection, IPacket>? _decryptionMethod;

    private Func<IPacket, IConnection, IPacket>? _compressionMethod;
    private Func<IPacket, IConnection, IPacket>? _decompressionMethod;

    /// <summary>
    /// The logger instance used for logging.
    /// </summary>
    /// <remarks>
    /// If not configured, logging may be disabled.
    /// </remarks>
    internal ILogger? Logger;

    /// <summary>
    /// A dictionary mapping packet command IDs (ushort) to their respective handlers.
    /// </summary>
    internal readonly Dictionary<ushort, Func<IPacket, IConnection, Task>> PacketHandlers = [];

    /// <summary>
    /// Indicates whether metrics tracking is enabled.
    /// </summary>
    private bool EnableMetrics { get; set; }

    /// <summary>
    /// Custom error handling strategy for packet processing.
    /// </summary>
    /// <remarks>
    /// If not set, the default behavior is to log errors.
    /// </remarks>
    internal Action<Exception, ushort>? ErrorHandler;

    /// <summary>
    /// Callback function to collect execution time metrics for packet processing.
    /// </summary>
    /// <remarks>
    /// The callback receives the packet handler name and execution time in milliseconds.
    /// </remarks>
    private Action<string, long>? MetricsCallback { get; set; }

    /// <summary>
    /// A function that serializes an <see cref="IPacket"/> into a <see cref="ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <remarks>
    /// This function is used to convert an <see cref="IPacket"/> object into a byte array representation
    /// for transmission over the network or for storage.
    /// </remarks>
    internal Func<IPacket, Memory<byte>>? SerializationMethod;

    /// <summary>
    /// A function that deserializes a <see cref="Memory{Byte}"/> into an <see cref="IPacket"/>.
    /// </summary>
    /// <remarks>
    /// This function is responsible for converting the byte array received over the network or from storage
    /// back into an <see cref="IPacket"/> object for further processing.
    /// </remarks>
    internal Func<ReadOnlyMemory<byte>, IPacket>? DeserializationMethod;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherOptions"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor sets up the default packet handler methods and initializes the dictionary
    /// that stores the handlers for various return types. It also prepares fields for encryption,
    /// decryption, serialization, and compression methods, which can later be customized using
    /// the appropriate configuration methods (e.g., <see cref="WithPacketCrypto"/> or
    /// <see cref="WithPacketSerialization"/>).
    /// </remarks>
    public PacketDispatcherOptions()
    {
    }

    #endregion

    #region Public Methods

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
    public PacketDispatcherOptions WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
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
            ushort commandId = method.GetCustomAttribute<PacketCommandAttribute>()!.CommandId;
            Type returnType = method.ReturnType;

            Func<object?, IPacket, IConnection, Task> value = GetHandler(returnType);

            if (PacketHandlers.ContainsKey(commandId))
            {
                throw new InvalidOperationException(
                    $"CommandId {commandId} is already registered by another handler.");
            }

            async Task Handler(IPacket packet, IConnection connection)
            {
                Stopwatch? stopwatch = null;

                try
                {
                    if (EnableMetrics)
                        stopwatch = Stopwatch.StartNew();

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
                            $"{typeof(TController).Name}.{method.Name}",
                            stopwatch.ElapsedMilliseconds);
                    }
                }
            }

            PacketHandlers[commandId] = Handler;
            registeredCommandIds.Add(commandId);
        }

        Logger?.Info($"Registered {typeof(TController).Name} for CommandIds: {string.Join(", ", registeredCommandIds)}");

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

    #region Obsolete Methods

    /// <summary>
    /// Configures packet compression and decompression for the packet dispatcher.
    /// </summary>
    /// <param name="compressionMethod">
    /// A function that compresses a packet before sending. The function receives an <see cref="IPacket"/>
    /// and returns the compressed <see cref="IPacket"/>. If this is null, compression will not be applied.
    /// </param>
    /// <param name="decompressionMethod">
    /// A function that decompresses a packet before processing. The function receives an <see cref="IPacket"/>
    /// and returns the decompressed <see cref="IPacket"/>. If this is null, decompression will not be applied.
    /// </param>
    /// <remarks>
    /// This method allows you to specify compression and decompression functions that will be applied to packets
    /// before they are sent or received. The compression and decompression methods are applied based on packet flags,
    /// which help determine if a packet should be compressed or decompressed. If either method is null, the corresponding
    /// compression or decompression step will be skipped.
    /// </remarks>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions"/> instance for method chaining.
    /// </returns>
    [Obsolete("Use WithTypedCompression and WithTypedDecompression for type-specific compression.")]
    public PacketDispatcherOptions WithPacketCompression
    (
        Func<IPacket, IConnection, IPacket>? compressionMethod,
        Func<IPacket, IConnection, IPacket>? decompressionMethod
    )
    {
        if (compressionMethod is not null) _compressionMethod = compressionMethod;
        if (decompressionMethod is not null) _decompressionMethod = decompressionMethod;

        Logger?.Debug("Packet compression configured.");
        return this;
    }

    /// <summary>
    /// Configures packet encryption and decryption for the packet dispatcher.
    /// </summary>
    /// <param name="encryptionMethod">
    /// A function that encrypts a packet before sending. The function receives an <see cref="IPacket"/> and a byte array (encryption key),
    /// and returns the encrypted <see cref="IPacket"/>.
    /// </param>
    /// <param name="decryptionMethod">
    /// A function that decrypts a packet before processing. The function receives an <see cref="IPacket"/> and a byte array (decryption key),
    /// and returns the decrypted <see cref="IPacket"/>.
    /// </param>
    /// <remarks>
    /// This method allows you to specify encryption and decryption functions that will be applied to packets
    /// before they are sent or received. The encryption and decryption methods will be invoked based on certain conditions,
    /// which are determined by the packet's flags (as checked by <see cref="IPacket.Flags"/>).
    /// Ensure that the encryption and decryption functions are compatible with the packet's structure.
    /// </remarks>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions"/> instance for method chaining.
    /// </returns>
    [Obsolete("Use WithTypedEncryption and WithTypedDecryption for type-specific encryption.")]
    public PacketDispatcherOptions WithPacketCrypto
    (
        Func<IPacket, IConnection, IPacket>? encryptionMethod,
        Func<IPacket, IConnection, IPacket>? decryptionMethod
    )
    {
        if (encryptionMethod is not null) _encryptionMethod = encryptionMethod;
        if (decryptionMethod is not null) _decryptionMethod = decryptionMethod;

        Logger?.Debug("Packet encryption configured.");
        return this;
    }

    /// <summary>
    /// Configures the packet serialization and deserialization methods.
    /// </summary>
    /// <param name="serializationMethod">
    /// A function that serializes a packet into a <see cref="Memory{Byte}"/>.
    /// </param>
    /// <param name="deserializationMethod">
    /// A function that deserializes a <see cref="Memory{Byte}"/> back into an <see cref="IPacket"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// This method allows customizing how packets are serialized before sending and deserialized upon receiving.
    /// </remarks>
    [Obsolete("Use WithTypedSerializer and WithTypedDeserializer for type-specific serialization.")]
    public PacketDispatcherOptions WithPacketSerialization
    (
        Func<IPacket, Memory<byte>>? serializationMethod,
        Func<ReadOnlyMemory<byte>, IPacket>? deserializationMethod
    )
    {
        if (serializationMethod is not null) SerializationMethod = serializationMethod;
        if (deserializationMethod is not null) DeserializationMethod = deserializationMethod;

        Logger?.Debug("Packet serialization configured.");
        return this;
    }

    #endregion

    #endregion

    #region Private Methods

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

    /// <summary>
    /// Handles serialization, encryption, and sending of an IPacket.
    /// </summary>
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

    /// <summary>
    /// Processes packet transformation based on a flag and a transformation method.
    /// </summary>
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

    #endregion
}
