using Nalix.Common.Logging;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Configures logging for the packet dispatcher, enabling logging of packet processing details.
    /// </summary>
    /// <param name="logger">The logger instance that will be used for logging packet processing events.</param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// The logger will be used to log various events such as packet handling, errors, and metrics if enabled.
    /// If logging is not configured, the dispatcher will not produce any logs.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithLogging(ILogger logger)
    {
        _logger = logger;
        _logger.Info("Logger instance successfully attached to PacketDispatch. Logging is now active.");

        return this;
    }

    /// <summary>
    /// Configures a custom error handler to manage exceptions during packet processing.
    /// </summary>
    /// <param name="errorHandler">
    /// An action that takes an exception and the packet ID, which allows custom handling of errors
    /// that occur while processing packets.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// This method allows you to define a custom error-handling strategy, such as logging errors,
    /// sending notifications, or taking corrective action in case of failures during packet processing.
    /// If no custom error handler is configured, the default behavior is to log the exception.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithErrorHandling(
        System.Action<System.Exception, System.UInt16> errorHandler)
    {
        _logger?.Info("Custom error handler has been set. All unhandled exceptions during packet processing will be routed.");
        _errorHandler = errorHandler;

        return this;
    }
}