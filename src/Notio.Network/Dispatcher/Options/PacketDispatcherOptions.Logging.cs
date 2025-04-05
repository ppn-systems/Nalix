using Notio.Common.Logging;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Enables metrics tracking and sets the callback function for reporting execution times.
    /// </summary>
    /// <param name="metricsCallback">The callback function receiving the handler name and execution time in milliseconds.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithMetrics(Action<string, long> metricsCallback)
    {
        IsMetricsEnabled = true;
        MetricsCallback = metricsCallback;
        _logger?.Debug("Metrics tracking enabled.");
        return this;
    }

    /// <summary>
    /// Configures logging for the packet dispatcher.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithLogging(ILogger logger)
    {
        _logger = logger;
        _logger.Debug("Logging configured.");
        return this;
    }

    /// <summary>
    /// Configures a custom error handler for exceptions occurring during packet processing.
    /// </summary>
    /// <param name="errorHandler">The error handler action.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithErrorHandling(Action<Exception, ushort> errorHandler)
    {
        ErrorHandler = errorHandler;
        _logger?.Debug("Error handler configured.");
        return this;
    }
}
