using Notio.Common.Connection;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Network.Package;
using Notio.Network.Package.Extensions;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

public sealed class PacketCommandRouter(ILogger? logger = null) : IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly ConcurrentDictionary<int, PacketHandlerInfo> _handlers = new();
    private readonly ConcurrentDictionary<Type, object> _instanceCache = new();
    private readonly SemaphoreSlim _routingLock = new(1, 1);
    private readonly Stopwatch _performanceMonitor = new();
    private bool _isDisposed;

    public void RegisterHandler<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods)] T>() where T : class
    {
        ArgumentNullException.ThrowIfNull(_logger);

        Type type = typeof(T);
        var controllerAttribute = type.GetCustomAttribute<PacketControllerAttribute>()
            ?? throw new InvalidOperationException($"Class {type.Name} must be marked with PacketControllerAttribute.");

        RegisterHandlerMethods(type, controllerAttribute);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _routingLock.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }

    private void RegisterHandlerMethods(Type type, PacketControllerAttribute controllerAttribute)
    {
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var method in methods)
        {
            var handlerAttribute = method.GetCustomAttribute<PacketCommandAttribute>();
            if (handlerAttribute == null) continue;

            ValidateMethodSignature(method);

            var handlerInfo = new PacketHandlerInfo(
                controllerAttribute,
                method,
                handlerAttribute.RequiredAuthority,
                type,
                IsAsyncMethod(method));

            if (!_handlers.TryAdd(handlerAttribute.CommandId, handlerInfo))
            {
                _logger?.Warn($"Command {handlerAttribute.CommandId} already has a handler.");
                continue;
            }

            _logger?.Info($"Registered {type.Name}.{method.Name} for command {handlerAttribute.CommandId}");
        }
    }

    private static void ValidateMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2 ||
            parameters[0].ParameterType != typeof(IConnection) ||
            parameters[1].ParameterType != typeof(Packet))
        {
            throw new InvalidOperationException(
                $"Handler method {method.Name} must have signature (IConnection, Packet)");
        }

        var returnType = method.ReturnType;
        if (returnType != typeof(Packet) &&
            returnType != typeof(Task<Packet>) &&
            returnType != typeof(ValueTask<Packet>))
        {
            throw new InvalidOperationException(
                $"Handler method {method.Name} must return Packet or Task<Packet> or ValueTask<Packet>");
        }
    }

    private static bool IsAsyncMethod(MethodInfo method)
        => method.ReturnType == typeof(Task<Packet>) ||
           method.ReturnType == typeof(ValueTask<Packet>);

    public async Task RoutePacketAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ThrowIfDisposed();

        await _routingLock.WaitAsync(cancellationToken);
        try
        {
            _performanceMonitor.Restart();

            var packet = connection.IncomingPacket.FromByteArray();

            if (!_handlers.TryGetValue(packet.Command, out var handlerInfo))
            {
                _logger?.Warn($"No handler found for command {packet.Command}");
                return;
            }

            if (!ValidateAuthority(connection, handlerInfo.RequiredAuthority))
            {
                _logger?.Warn($"Access denied for command {packet.Command}. " +
                            $"Required: {handlerInfo.RequiredAuthority}, User: {connection.Authority}");
                return;
            }

            var instance = GetOrCreateInstance(handlerInfo.ControllerType);
            Packet? response = await InvokeHandlerMethodAsync(handlerInfo, instance, connection, packet);

            if (response != null)
            {
                await connection.SendAsync(response.Value.ToByteArray(), cancellationToken);
                LogPerformanceMetrics(packet.Command);
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

    private object GetOrCreateInstance(Type type)
        => _instanceCache.GetOrAdd(type, t => Activator.CreateInstance(t)!);

    private async Task<Packet?> InvokeHandlerMethodAsync(
        PacketHandlerInfo handlerInfo, object instance,
        IConnection connection, Packet packet)
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
                return (Packet?)result;
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error executing command {packet.Command}: {ex}");
            return null;
        }
    }

    private void LogPerformanceMetrics(int command)
    {
        _performanceMonitor.Stop();
        _logger?.Debug($"Command {command} processed in {_performanceMonitor.ElapsedMilliseconds}ms");
    }

    private static bool ValidateAuthority(IConnection connection, Authoritys required)
        => connection.Authority >= required;

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PacketCommandRouter));
}