using Notio.Common;
using Notio.Common.Connection;
using Notio.Network.Handlers.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Notio.Network.Handlers;

/// <summary>
/// Contains options for configuring an instance of <see cref="PacketDispatcher"/>.
/// </summary>
public class PacketDispatcherOptions
{
    internal readonly Dictionary<ushort, Action<IPacket, IConnection>> PacketHandlers = [];

    /// <summary>
    /// Registers controller types automatically, finding methods with the PacketCommandAttribute.
    /// </summary>
    public PacketDispatcherOptions WithHandler<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
            where TController : new()
    {
        // Get methods from the controller that are decorated with PacketCommandAttribute
        List<MethodInfo> methods = typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketCommandAttribute>() != null)
            .ToList();

        // For each method found, register it with the corresponding commandId
        foreach (MethodInfo method in methods)
        {
            ushort commandId = method.GetCustomAttribute<PacketCommandAttribute>()!.CommandId;
            TController controller = new();

            // Check if the method returns byte[] or void
            if (method.ReturnType == typeof(byte[]))
            {
                PacketHandlers[commandId] = (packet, connection) =>
                {
                    if (method.Invoke(controller, [packet, connection]) is byte[] result)
                        connection.Send(result);
                };
            }
            else if (method.ReturnType == typeof(IEnumerable<byte>))
            {
                PacketHandlers[commandId] = (packet, connection) =>
                {
                    if (method.Invoke(controller, [packet, connection]) is IEnumerable<byte> result)
                        connection.Send(result.ToArray());
                };
            }
            else if (method.ReturnType == typeof(void))
            {
                // Default handling for void return methods
                PacketHandlers[commandId] = (packet, connection) =>
                {
                    method.Invoke(controller, [packet, connection]);
                };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported return type: {method.ReturnType}");
            }
        }

        return this;
    }
}
