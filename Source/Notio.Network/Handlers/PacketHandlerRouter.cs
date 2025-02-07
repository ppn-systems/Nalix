using Notio.Common.Connection;
using Notio.Common.Diagnostics;
using Notio.Common.Exceptions;
using Notio.Common.Injection;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Network.Handlers.Metadata;
using Notio.Network.Package;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

public sealed class PacketHandlerRouter(ILogger? logger = null) : IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly InstanceManager _instanceManager = new();
    private readonly PerformanceMonitor _performanceMonitor = new();
    private readonly PacketHandlerResolver _handlerResolver = new(logger);

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void RegisterHandler<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods)] T>() where T : class
    {
        ArgumentNullException.ThrowIfNull(_logger);

        Type type = typeof(T);
        var controllerAttribute = type.GetCustomAttribute<PacketControllerAttribute>()
            ?? throw new InvalidOperationException($"Class {type.Name} must be marked with PacketControllerAttribute.");

        _handlerResolver.RegisterHandlers(type, controllerAttribute);
    }

    public async Task RoutePacketAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ThrowIfDisposed();

        // Use a local performance monitor if needed
        var performanceMonitor = new PerformanceMonitor();
        performanceMonitor.Start();

        try
        {
            var packet = connection.IncomingPacket.Deserialize();

            if (!_handlerResolver.TryGetHandler(packet.Command, out PacketHandlerInfo? handlerInfo)
                || handlerInfo == null)
            {
                _logger?.Warn($"No handler found for command <{packet.Command}>");
                return;
            }

            if (!ValidateAuthority(connection, handlerInfo.RequiredAuthority))
            {
                _logger?.Warn($"Access denied for command <{packet.Command}>. Required: <{handlerInfo.RequiredAuthority}>, User: <{connection.Authority}>");
                return;
            }

            var instance = _instanceManager.GetOrCreateInstance(handlerInfo.ControllerType);
            var response = await InvokeHandlerMethodAsync(handlerInfo, instance, connection, packet);

            if (response != null)
            {
                await connection.SendAsync(response.Value.Serialize(), cancellationToken);
                performanceMonitor.Stop();
                _logger?.Debug($"Command <{packet.Command}> processed in {performanceMonitor.ElapsedMilliseconds}ms");
            }
        }
        catch (PackageException ex)
        {
            _logger?.Warn($"ID:{connection.Id}/IP:{connection.RemoteEndPoint}/Er:{ex}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"Error processing packet: {ex}");
        }
    }

    private async Task<Packet?> InvokeHandlerMethodAsync(PacketHandlerInfo handlerInfo, object instance, IConnection connection, Packet packet)
    {
        try
        {
            var result = handlerInfo.Method.Invoke(instance, [connection, packet]);

            if (handlerInfo.IsAsync)
            {
                if (result is Task<Packet> taskResult)
                    return await taskResult;
                else if (result is ValueTask<Packet> valueTaskResult)
                    return await valueTaskResult;
                else
                    throw new InvalidOperationException("Async method did not return Task<Packet> or ValueTask<Packet>");
            }
            else
            {
                return (Packet?)result;
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error executing command {packet.Command}: {ex}");
            return null;
        }
    }

    private static bool ValidateAuthority(IConnection connection, Authoritys required)
        => connection.Authority >= required;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PacketHandlerRouter));
}