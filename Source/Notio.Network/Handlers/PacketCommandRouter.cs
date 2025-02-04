using Notio.Common.Connection;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Models;
using Notio.Network.Package;
using Notio.Network.Package.Extensions;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Notio.Network.Handlers;

/// <summary>
/// Represents a router for handling packet commands.
/// </summary>
public class PacketCommandRouter(ILogger? logger = null)
{
    private readonly ILogger? _logger = logger;

    private readonly ConcurrentDictionary
        <int, (PacketControllerAttribute handler, MethodInfo method, Authoritys requiredAuthority)> _handlers = new();

    /// <summary>
    /// Registers a handler by passing in the Type.
    /// </summary>
    public void RegisterHandler<
        [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods)] T>()
        where T : class, new()
    {
        // Get class type
        Type type = typeof(T);

        // Look for PacketControllerAttribute on the class
        var controllerAttribute = type.GetCustomAttribute<PacketControllerAttribute>();
        if (controllerAttribute == null)
        {
            _logger?.Warn($"Class {type.Name} is not marked with PacketControllerAttribute.");
            return;
        }

        // Register methods with PacketHandlerAttribute
        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Static
        );
        foreach (var method in methods)
        {
            var handlerAttribute = method.GetCustomAttribute<PacketCommandAttribute>();
            if (handlerAttribute != null)
            {
                if (_handlers.TryAdd(handlerAttribute.CommandId,
                    (controllerAttribute, method, handlerAttribute.RequiredAuthority)))
                {
                    _logger?.Info(
                        $"Registered {type.Name}.{method.Name} " +
                        $"for command {handlerAttribute.CommandId}");
                }
                else
                {
                    _logger?.Warn($"Command {handlerAttribute.CommandId} already has a handler.");
                }
            }
        }
    }

    /// <summary>
    /// Processes a packet based on the command and checks authority.
    /// </summary>
    public void RoutePacket(IConnection connection)
    {
        if (connection == null)
        {
            _logger?.Error("Connection is null");
            return;
        }

        Packet packet = connection.IncomingPacket.FromByteArray();
        if (!_handlers.TryGetValue(packet.Command, out var handlerInfo))
        {
            _logger?.Warn($"No handler found for command {packet.Command}");
            return;
        }

        var (handler, method, requiredAuthority) = handlerInfo;
        if (!ValidateAuthority(connection, requiredAuthority))
        {
            _logger?.Warn(
                $"Access denied for command {packet.Command}. " +
                $"Required authority: {requiredAuthority}, " +
                $"User authority: {connection.Authority}");
            return;
        }

        try
        {
            if (method.Invoke(handler, [connection, packet]) is Packet packetResponse)
            {
                connection.Send(packetResponse.ToByteArray());
                _logger?.Info($"Command {packet.Command} processed successfully.");
            }
            else
            {
                _logger?.Warn($"Method {method.Name} returned null for command {packet.Command}");
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error executing command {packet.Command}: {ex.Message}");
        }
    }

    private static bool ValidateAuthority(IConnection connection, Authoritys required)
        => connection != null && connection.Authority >= required;
}