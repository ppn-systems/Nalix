using Notio.Common.Attributes;
using Notio.Common.Connection;
using Notio.Common.Data;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

/// <summary>
/// Provides configuration options for an instance of <see cref="PacketDispatcher"/>.
/// </summary>
/// <remarks>
/// This class allows registering packet handlers, configuring logging, and defining error-handling strategies.
/// </remarks>
public sealed class PacketDispatcherOptions
{
    private readonly Dictionary<Type, Func<object?, IPacket, IConnection, Task>> _methodHandlers;

    private Func<IPacket, IConnection, IPacket>? _encryptionMethod;
    private Func<IPacket, IConnection, IPacket>? _decryptionMethod;

    private Func<IPacket, IPacket>? _compressionMethod;
    private Func<IPacket, IPacket>? _decompressionMethod;

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
    /// Custom error handling strategy for packet processing.
    /// </summary>
    /// <remarks>
    /// If not set, the default behavior is to log errors.
    /// </remarks>
    internal Action<Exception, ushort>? ErrorHandler;

    /// <summary>
    /// Indicates whether metrics tracking is enabled.
    /// </summary>
    private bool EnableMetrics { get; set; }

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
        _methodHandlers = new Dictionary<Type, Func<object?, IPacket, IConnection, Task>>
        {
            [typeof(void)] = (_, _, _) => Task.CompletedTask,

            [typeof(byte[])] = async (result, _, connection) =>
            {
                if (result is byte[] data)
                {
                    using MemoryStream ms = new();

                    //byte[] lengthBytes = new byte[2];

                    //BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)(data.Length + 2));

                    //await ms.WriteAsync(lengthBytes);
                    await ms.WriteAsync(data);

                    await connection.SendAsync(ms.ToArray());
                }
            },

            [typeof(IEnumerable<byte>)] = async (result, _, connection) =>
            {
                if (result is IEnumerable<byte> bytes)
                {
                    using MemoryStream ms = new();

                    byte[] data = [.. bytes];
                    //byte[] lengthBytes = new byte[2];

                    //BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)(data.Length + 2));

                    //await ms.WriteAsync(lengthBytes);
                    await ms.WriteAsync(data);

                    await connection.SendAsync(ms.ToArray());
                }
            },

            [typeof(IPacket)] = async (result, _, connection) =>
            {
                if (result is IPacket packet)
                {
                    if (this.SerializationMethod is null)
                    {
                        if (this.Logger is not null)
                            this.Logger.Error("Serialization method is not set.");
                        else
                            throw new InvalidOperationException("Serialization method is not set.");

                        return;
                    }

                    if (this._compressionMethod is not null)
                        packet = this._compressionMethod(packet);

                    if (this._encryptionMethod is not null)
                        packet = this._encryptionMethod(packet, connection);

                    await connection.SendAsync(this.SerializationMethod(packet));
                }
            },

            [typeof(Task)] = async (result, _, _) => await (Task)result!,

            [typeof(Task<byte[]>)] = async (result, _, connection) =>
            {
                if (result is Task<byte[]> task)
                {
                    byte[] data = await task;
                    using var ms = new MemoryStream();

                    byte[] lengthBytes = new byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)(data.Length + 2));

                    await ms.WriteAsync(lengthBytes);
                    await ms.WriteAsync(data);

                    await connection.SendAsync(ms.ToArray());
                }
            },

            [typeof(Task<IEnumerable<byte>>)] = async (result, _, connection) =>
            {
                if (result is Task<IEnumerable<byte>> task)
                {
                    using MemoryStream ms = new();

                    IEnumerable<byte> taskResult = await task;

                    byte[] data = [.. taskResult];
                    byte[] lengthBytes = new byte[2];

                    BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)(data.Length + 2));

                    await ms.WriteAsync(lengthBytes);
                    await ms.WriteAsync(data);

                    await connection.SendAsync(ms.ToArray());
                }
            },

            [typeof(Task<IPacket>)] = async (result, _, connection) =>
            {
                IPacket packet = await (Task<IPacket>)result!;

                if (this.SerializationMethod is null)
                {
                    if (this.Logger is not null)
                        this.Logger.Error("Serialization method is not set.");
                    else
                        throw new InvalidOperationException("Serialization method is not set.");

                    return;
                }

                if (this._compressionMethod is not null)
                    packet = this._compressionMethod(packet);

                if (this._encryptionMethod is not null)
                    packet = this._encryptionMethod(packet, connection);

                await connection.SendAsync(this.SerializationMethod(packet));
            }
        };
    }

    /// <summary>
    /// Enables metrics tracking and sets the callback function for reporting execution times.
    /// </summary>
    /// <param name="metricsCallback">The callback function receiving the handler name and execution time in milliseconds.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    public PacketDispatcherOptions WithMetrics(Action<string, long> metricsCallback)
    {
        EnableMetrics = true;
        MetricsCallback = metricsCallback;
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
        => this.WithHandler(() => new TController());

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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>(Func<TController> factory)
        where TController : class
    {
        TController controllerInstance = factory();

        // Get methods from the controller that are decorated with PacketCommandAttribute
        List<MethodInfo> methods = [.. typeof(TController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Where(m => m.GetCustomAttribute<PacketCommandAttribute>() != null)];

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketCommandAttribute>()!.CommandId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        IEnumerable<ushort> commandIds = duplicateCommandIds as ushort[] ?? [.. duplicateCommandIds];
        if (commandIds.Any())
            throw new InvalidOperationException(
                $"Duplicate CommandIds found: {string.Join(", ", commandIds)}");

        // Register each method with its corresponding commandId
        foreach (MethodInfo method in methods)
        {
            ushort commandId = method.GetCustomAttribute<PacketCommandAttribute>()!.CommandId;
            Type returnType = method.ReturnType;

            if (!_methodHandlers.TryGetValue(returnType, out Func<object?, IPacket, IConnection, Task>? value))
            {
                throw new InvalidOperationException(
                    $"Method {method.Name} has unsupported return type: {returnType}");
            }

            async Task Handler(IPacket packet, IConnection connection)
            {
                Stopwatch? stopwatch = null;

                try
                {
                    if (EnableMetrics)
                        stopwatch = Stopwatch.StartNew();

                    TController controller = Activator.CreateInstance<TController>();
                    object? result;

                    try
                    {
                        if (_decompressionMethod != null)
                            packet = _decompressionMethod(packet);

                        if (_decryptionMethod != null)
                            packet = _decryptionMethod(packet, connection);

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
        }

        return this;
    }

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
    public PacketDispatcherOptions WithPacketCompression
    (
        Func<IPacket, IPacket>? compressionMethod,
        Func<IPacket, IPacket>? decompressionMethod
    )
    {
        if (compressionMethod is not null) _compressionMethod = compressionMethod;
        if (decompressionMethod is not null) _decompressionMethod = decompressionMethod;

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
    public PacketDispatcherOptions WithPacketCrypto
    (
        Func<IPacket, IConnection, IPacket>? encryptionMethod,
        Func<IPacket, IConnection, IPacket>? decryptionMethod
    )
    {
        if (encryptionMethod is not null) _encryptionMethod = encryptionMethod;
        if (decryptionMethod is not null) _decryptionMethod = decryptionMethod;

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
    public PacketDispatcherOptions WithPacketSerialization
    (
        Func<IPacket, Memory<byte>>? serializationMethod,
        Func<ReadOnlyMemory<byte>, IPacket>? deserializationMethod
    )
    {
        if (serializationMethod is not null) SerializationMethod = serializationMethod;
        if (deserializationMethod is not null) DeserializationMethod = deserializationMethod;

        return this;
    }
}
