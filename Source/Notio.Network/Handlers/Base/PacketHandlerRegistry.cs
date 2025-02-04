using Notio.Common.Connection;
using Notio.Common.Logging.Interfaces;
using Notio.Network.Package;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Notio.Network.Handlers;

internal class PacketHandlerRegistry(ILogger? logger = null)
{
    private readonly ILogger? _logger = logger;
    private readonly ConcurrentDictionary<int, PacketHandlerInfo> _handlers = new();

    public void RegisterHandlerMethods(Type type, PacketControllerAttribute controllerAttribute)
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
            }
            else
            {
                _logger?.Info($"Registered {type.Name}.{method.Name} for command {handlerAttribute.CommandId}");
            }
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

    public bool TryGetHandler(int commandId, out PacketHandlerInfo handlerInfo)
    {
        return _handlers.TryGetValue(commandId, out handlerInfo);
    }
}