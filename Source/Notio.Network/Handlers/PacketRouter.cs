using Notio.Common.Connection;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Network.Handlers.Base;
using Notio.Network.Package;
using Notio.Network.Package.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

public sealed class PacketRouter(ILogger? logger = null) : IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly SemaphoreSlim _routingLock = new(1, 1);
    private readonly InstanceManager _instanceManager = new();
    private readonly PerformanceMonitor _performanceMonitor = new();
    private readonly PacketHandlerRegistry _handlerRegistry = new(logger);

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed) return;
        _routingLock.Dispose();
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

        _handlerRegistry.RegisterHandlerMethods(type, controllerAttribute);
    }

    public async Task RoutePacketAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ThrowIfDisposed();

        await _routingLock.WaitAsync(cancellationToken);
        try
        {
            _performanceMonitor.Start();

            var packet = connection.IncomingPacket.FromByteArray();

            if (!_handlerRegistry.TryGetHandler(packet.Command, out PacketHandlerInfo? handlerInfo)
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
                await connection.SendAsync(response.Value.ToByteArray(), cancellationToken);
                _performanceMonitor.Stop();
                _logger?.Debug($"Command <{packet.Command}> processed in {_performanceMonitor.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"Error processing packet: {ex}");
        }
        finally
        {
            _routingLock.Release();
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

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PacketRouter));
}