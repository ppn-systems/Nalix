using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher.Options;

/// <summary>
/// Provides configuration options for an instance of <see cref="PacketDispatcher{TPacket}"/>.
/// </summary>
/// <remarks>
/// This class allows registering packet handlers, configuring logging, and defining error-handling strategies.
/// </remarks>
public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : class
{
    #region Const

    private const DynamicallyAccessedMemberTypes RequiredMembers =
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    #endregion

    #region Fields

    private ILogger? _logger;

    private Func<TPacket, IConnection, TPacket>? _encryptionMethod;
    private Func<TPacket, IConnection, TPacket>? _decryptionMethod;

    private Func<TPacket, IConnection, TPacket>? _compressionMethod;
    private Func<TPacket, IConnection, TPacket>? _decompressionMethod;

    /// <summary>
    /// Indicates whether metrics tracking is enabled.
    /// </summary>
    private bool IsMetricsEnabled { get; set; }

    /// <summary>
    /// Custom error handling strategy for packet processing.
    /// </summary>
    /// <remarks>
    /// If not set, the default behavior is to log errors.
    /// </remarks>
    private Action<Exception, ushort>? ErrorHandler;

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
    private Func<TPacket, Memory<byte>>? SerializationMethod;

    /// <summary>
    /// A function that deserializes a <see cref="Memory{Byte}"/> into an <see cref="IPacket"/>.
    /// </summary>
    /// <remarks>
    /// This function is responsible for converting the byte array received over the network or from storage
    /// back into an <see cref="IPacket"/> object for further processing.
    /// </remarks>
    private Func<ReadOnlyMemory<byte>, TPacket>? DeserializationMethod;

    /// <summary>
    /// A dictionary mapping packet command IDs (ushort) to their respective handlers.
    /// </summary>
    private readonly Dictionary<ushort, Func<TPacket, IConnection, Task>> PacketHandlers = [];

    /// <summary>
    /// The logger instance used for logging.
    /// </summary>
    /// <remarks>
    /// If not configured, logging may be disabled.
    /// </remarks>
    public ILogger? Logger => _logger;

    /// <summary>
    /// Checks if encryption is enabled.
    /// </summary>
    public bool IsEncryptionEnabled => _encryptionMethod != null && _decryptionMethod != null;

    /// <summary>
    /// Checks if compression is enabled.
    /// </summary>
    public bool IsCompressionEnabled => _compressionMethod != null && _decompressionMethod != null;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherOptions{TPacket}"/> class.
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
}
