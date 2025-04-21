using Nalix.Common.Logging;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Enables metrics tracking for packet processing, allowing you to monitor execution times of handlers.
    /// </summary>
    /// <param name="metricsCallback">
    /// A callback function that receives the handler name and execution time in milliseconds.
    /// This callback is invoked each time a packet handler is executed, providing performance insights.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance to allow method chaining.
    /// </returns>
    /// <remarks>
    /// Enabling this feature helps to track how long packet handlers take to execute, which can be useful
    /// for performance monitoring and optimization. The callback provides a way to record or process these metrics.
    /// </remarks>
    public PacketDispatchOptions<TPacket> WithMetrics(System.Action<string, long> metricsCallback)
    {
        _logger?.Info("Packet metrics tracking has been enabled. Execution time will be logged per handler.");

        _isMetricsEnabled = true;
        _metricsCallback = metricsCallback;

        return this;
    }

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
    public PacketDispatchOptions<TPacket> WithErrorHandling(System.Action<System.Exception, ushort> errorHandler)
    {
        _logger?.Info("Custom error handler has been set. All unhandled exceptions during packet processing will be routed.");
        _errorHandler = errorHandler;

        return this;
    }
}
