using Notio.Common.Logging;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket
    : IPacket, IPacketCompressor<TPacket>, IPacketEncryptor<TPacket>
{
    /// <summary>
    /// Enables metrics tracking for packet processing, allowing you to monitor execution times of handlers.
    /// </summary>
    /// <param name="metricsCallback">
    /// A callback function that receives the handler name and execution time in milliseconds.
    /// This callback is invoked each time a packet handler is executed, providing performance insights.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance to allow method chaining.
    /// </returns>
    /// <remarks>
    /// Enabling this feature helps to track how long packet handlers take to execute, which can be useful
    /// for performance monitoring and optimization. The callback provides a way to record or process these metrics.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithMetrics(Action<string, long> metricsCallback)
    {
        _logger?.Info("Packet metrics tracking has been enabled. Execution time will be logged per handler.");
        IsMetricsEnabled = true;
        MetricsCallback = metricsCallback;

        return this;
    }

    /// <summary>
    /// Configures logging for the packet dispatcher, enabling logging of packet processing details.
    /// </summary>
    /// <param name="logger">The logger instance that will be used for logging packet processing events.</param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// The logger will be used to log various events such as packet handling, errors, and metrics if enabled.
    /// If logging is not configured, the dispatcher will not produce any logs.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithLogging(ILogger logger)
    {
        _logger = logger;
        _logger.Info("Logger instance successfully attached to PacketDispatcher. Logging is now active.");

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
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// This method allows you to define a custom error-handling strategy, such as logging errors,
    /// sending notifications, or taking corrective action in case of failures during packet processing.
    /// If no custom error handler is configured, the default behavior is to log the exception.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithErrorHandling(Action<Exception, ushort> errorHandler)
    {
        _logger?.Info("Custom error handler has been set. All unhandled exceptions during packet processing will be routed.");
        ErrorHandler = errorHandler;

        return this;
    }
}
