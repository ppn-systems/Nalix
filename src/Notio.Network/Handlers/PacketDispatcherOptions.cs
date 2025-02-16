using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Network.Handlers.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
public class PacketDispatcherOptions
{
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
    internal bool EnableMetrics { get; set; }

    /// <summary>
    /// Callback function to collect execution time metrics for packet processing.
    /// </summary>
    /// <remarks>
    /// The callback receives the packet handler name and execution time in milliseconds.
    /// </remarks>
    internal Action<string, long>? MetricsCallback { get; set; }

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
    /// Registers a handler by scanning the specified controller type for methods
    /// decorated with <see cref="PacketCommandAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">The type of the controller to register.</typeparam>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatcherOptions WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
        where TController : new()
    {
        // Get methods from the controller that are decorated with PacketCommandAttribute
        Lazy<TController> lazyController = new(() => new TController());

        List<MethodInfo> methods = [.. typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketCommandAttribute>() != null)];

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketCommandAttribute>()!.CommandId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
            throw new InvalidOperationException(
                $"Duplicate CommandIds found: {string.Join(", ", duplicateCommandIds)}");

        // Register each method with its corresponding commandId
        foreach (MethodInfo method in methods)
        {
            ushort commandId = method.GetCustomAttribute<PacketCommandAttribute>()!.CommandId;
            Type returnType = method.ReturnType;

            if (!MethodHandlers.TryGetValue(returnType, out Func<object?, IPacket, IConnection, Task>? value))
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
                    {
                        stopwatch = Stopwatch.StartNew();
                    }

                    TController controller = lazyController.Value;
                    object? result;

                    try
                    {
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

    public PacketDispatcherOptions WithPacketCtypto(
        Func<IPacket, IPacket> encryptionMethod,
        Func<IPacket, IPacket> decryptionMethod)
    {
        return this;
    }

    private static readonly Dictionary<Type, Func<object?, IPacket, IConnection, Task>> MethodHandlers = new()
    {
        [typeof(Task)] = async (result, _, _) =>
            await (Task)result!,

        [typeof(byte[])] = async (result, _, connection) =>
            await connection.SendAsync((byte[])result!),

        [typeof(IEnumerable<byte>)] = async (result, _, connection) =>
            await connection.SendAsync(((IEnumerable<byte>)result!).ToArray()),

        [typeof(void)] = (_, _, _) =>
            Task.CompletedTask
    };
}
