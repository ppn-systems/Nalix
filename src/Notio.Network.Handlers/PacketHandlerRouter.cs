using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Diagnostics;
using Notio.Network.Package;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

/// <summary>
/// Ultra-high performance packet router with advanced DI integration and async support
/// </summary>
public sealed class PacketHandlerRouter(ILogger? logger = null, IServiceProvider? serviceProvider = null) : IDisposable
{
    private readonly ILogger? _logger = logger;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    private readonly ConcurrentDictionary<Type, Delegate> _handlerCache = new();
    private readonly ConcurrentDictionary<ushort, PacketHandlerInfo> _handlers = new();

    private bool _isDisposed;

    /// <summary>
    /// Advanced handler registration with built-in compilation
    /// </summary>
    public void RegisterHandler<[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class
    {
        ThrowIfDisposed();

        var type = typeof(T);
        _ = type.GetCustomAttribute<PacketControllerAttribute>()
            ?? throw new InvalidOperationException($"Controller {type.Name} requires PacketControllerAttribute");

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attribute = method.GetCustomAttribute<PacketCommandAttribute>();
            if (attribute == null) continue;

            ValidateMethodSignature(method);
            CompileHandlerMethod(type, method, attribute);
        }
    }

    /// <summary>
    /// High-performance async packet routing
    /// </summary>
    public async Task RoutePacketAsync(IConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ThrowIfDisposed();

        using PerformanceMonitor perfMonitor = new();

        try
        {
            perfMonitor.Start();

            Packet packet = connection.IncomingPacket?.Deserialize()
                ?? throw new InvalidOperationException("Failed to deserialize packet.");

            if (!_handlers.TryGetValue(packet.Command, out var handler))
            {
                _logger?.Warn("No handler for command {Command}", packet.Command);
                return;
            }

            if (!ValidateAuthority(connection, handler.RequiredAuthority))
            {
                _logger?.Warn("Authority mismatch for {Command}", packet.Command);
                return;
            }

            var instance = CreateHandlerInstance(handler.ControllerType);
            var response = await handler.Handler(instance, connection, packet, cancellationToken)
                .ConfigureAwait(false);

            if (response.HasValue)
            {
                connection.Send(response.Value.Serialize());
            }

            _logger?.Debug($"Processed {packet.Command} in {perfMonitor.ElapsedMilliseconds}ms");
        }
        catch (InvalidOperationException)
        {
        }
        catch (PackageException ex)
        {
            _logger?.Warn($"IP:{connection.RemoteEndPoint}-Error:{ex}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"IP:{connection.RemoteEndPoint}-Error processing packet", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _handlers.Clear();
        _handlerCache.Clear();
        GC.SuppressFinalize(this);
    }

    private void CompileHandlerMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type, MethodInfo method, PacketCommandAttribute attribute)
    {
        var commandId = attribute.CommandId;
        var parameters = method.GetParameters();

        // Compile ultra-fast execution path
        var handlerDelegate = CreateHandlerDelegate(method);
        var handlerInfo = new PacketHandlerInfo(
            type,
            attribute.RequiredAuthority,
            handlerDelegate
        );

        if (!_handlers.TryAdd(commandId, handlerInfo))
        {
            throw new InvalidOperationException($"Duplicate handler for command {commandId}");
        }

        _logger?.Info("Registered {Method} for command {Command}",
            method.Name, commandId);
    }

    private Func<object, IConnection, Packet, CancellationToken, Task<Packet?>> CreateHandlerDelegate(MethodInfo method)
    {
        if (_handlerCache.TryGetValue(method.DeclaringType!, out var cachedDelegate))
        {
            return (Func<object, IConnection, Packet, CancellationToken, Task<Packet?>>)cachedDelegate;
        }

        // Expression tree compilation for maximum performance
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var connectionParam = Expression.Parameter(typeof(IConnection), "connection");
        var packetParam = Expression.Parameter(typeof(Packet), "packet");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var instanceCast = Expression.Convert(instanceParam, method.DeclaringType!);

        var methodCall = method.GetParameters().Length switch
        {
            2 => Expression.Call(instanceCast, method, connectionParam, packetParam),
            3 => Expression.Call(instanceCast, method, connectionParam, packetParam, cancellationTokenParam),
            _ => throw new InvalidOperationException("Invalid parameter count")
        };

        Expression<Func<object, IConnection, Packet, CancellationToken, Task<Packet?>>> lambda;

        if (method.ReturnType == typeof(Task<Packet>))
        {
            lambda = Expression.Lambda<Func<object, IConnection, Packet, CancellationToken, Task<Packet?>>>(
                Expression.Convert(methodCall, typeof(Task<Packet?>)),
                instanceParam, connectionParam, packetParam, cancellationTokenParam);
        }
        else if (method.ReturnType == typeof(Packet))
        {
            var taskResult = Expression.Call(typeof(Task), "FromResult", null,
                Expression.Convert(methodCall, typeof(Packet?)));
            lambda = Expression.Lambda<Func<object, IConnection, Packet, CancellationToken, Task<Packet?>>>(
                taskResult, instanceParam, connectionParam, packetParam, cancellationTokenParam);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported return type {method.ReturnType}");
        }

        var compiled = lambda.Compile();
        _handlerCache.TryAdd(method.DeclaringType!, compiled);
        return compiled;
    }

    private object CreateHandlerInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type controllerType)
        => _serviceProvider?.GetService(controllerType) ??
               Activator.CreateInstance(controllerType) ??
               throw new InvalidOperationException($"Cannot create instance of {controllerType.Name}.");

    private static void ValidateMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var isValid = parameters.Length switch
        {
            2 => parameters[0].ParameterType == typeof(IConnection) &&
                 parameters[1].ParameterType == typeof(Packet),
            3 => parameters[0].ParameterType == typeof(IConnection) &&
                 parameters[1].ParameterType == typeof(Packet) &&
                 parameters[2].ParameterType == typeof(CancellationToken),
            _ => false
        };

        if (!isValid || (method.ReturnType != typeof(Packet) &&
                        method.ReturnType != typeof(Task<Packet>)))
        {
            throw new InvalidOperationException(
                $"Method {method.Name} in {method.DeclaringType?.Name} has an invalid signature. " +
                "Expected: (IConnection, Packet) or (IConnection, Packet, CancellationToken) " +
                "with return type Packet or Task<Packet>."
            );
        }
    }

    private static bool ValidateAuthority(IConnection connection, Authoritys required)
        => connection.Authority >= required;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, nameof(PacketHandlerRouter));
}

internal sealed class PacketHandlerInfo(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type controllerType,
    Authoritys requiredAuthority,
    Func<object, IConnection, Packet, CancellationToken, Task<Packet?>> handler)
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type ControllerType { get; } = controllerType;

    public Authoritys RequiredAuthority { get; } = requiredAuthority;
    public Func<object, IConnection, Packet, CancellationToken, Task<Packet?>> Handler { get; } = handler;
}
