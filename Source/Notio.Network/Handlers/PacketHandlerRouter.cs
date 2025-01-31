using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Package;
using Notio.Package.Extensions;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Notio.Network.Handlers;

/// <summary>
/// Represents a router for handling packet commands.
/// </summary>
public class PacketHandlerRouter(ILogger? logger)
{
    private readonly ILogger? _logger = logger;
    private readonly ConcurrentDictionary<short, (PacketController, MethodInfo, Authoritys)> _handlers = new();

    /// <summary>
    /// Registers a handler by passing in the Type.
    /// </summary>
    public void RegisterHandler<T>() where T : PacketController, new()
        => RegisterHandlerInstance(new T());

    /// <summary>
    /// Processes a packet based on the command and checks authority.
    /// </summary>
    public void RoutePacket(IConnection connection)
    {
        Packet packet = connection.IncomingPacket.FromByteArray();

        if (!_handlers.TryGetValue(packet.Command, out var handlerInfo))
        {
            _logger?.Warn($"No handler found for command {packet.Command}");
            return;
        }

        var (handler, method, requiredAuthority) = handlerInfo;

        if (!ValidateAuthority(connection, requiredAuthority))
        {
            _logger?.Warn($"Access denied for command {packet.Command}. Required authority: {requiredAuthority}, User authority: {connection.Authority}");
            return;
        }

        try
        {
            if (method.Invoke(handler, [connection, packet]) is Packet packetResponse)
            {
                connection.Send(packetResponse.ToByteArray());
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error executing command {packet.Command}: {ex.Message}");
        }
    }

    internal void RegisterHandlerInstance(PacketController handler)
    {
        Type type = handler.GetType();
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            PacketCommandAttribute? attr = method.GetCustomAttribute<PacketCommandAttribute>();
            if (attr == null) continue;

            if (method.ReturnType != typeof(byte[]))
            {
                _logger?.Warn($"Method {method.Name} in {type.Name} must return byte[]");
                continue;
            }

            if (_handlers.TryAdd(attr.CommandId, (handler, method, attr.RequiredAuthority)))
            {
                _logger?.Info($"Registered {type.Name}.{method.Name} for command {attr.CommandId} with authority {attr.CommandId}");
            }
            else
            {
                _logger?.Warn($"Command {attr.CommandId} already has a handler!");
            }
        }
    }

    private static bool ValidateAuthority(IConnection connection, Authoritys required)
        => connection != null && connection.Authority >= required;
}