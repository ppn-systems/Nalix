// File: Nalix.Network/Internal/Compilation/HandlerPrecompiler.cs
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Internal.Compilation;
using System.Linq;

namespace Nalix.Network.Registry;

/// <summary>
/// Precompiles all packet handlers for JIT-capable runtimes to reduce first-hit latency.
/// Non-invasive: uses HandlerCompiler under the hood.
/// </summary>
public static class HandlerPrecompiler
{
    /// <summary>
    /// Scan an assembly for types tagged with [PacketController] and precompile handlers for TPacket.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<System.Object> PrecompileAssembly<TPacket>(
        System.Reflection.Assembly asm,
        System.Func<System.Type, System.Object> controllerFactory)
        where TPacket : IPacket
    {
        System.ArgumentNullException.ThrowIfNull(asm);
        System.Collections.Generic.List<System.Object> created = new(64);

        // Identify controllers by PacketControllerAttribute:contentReference[oaicite:12]{index=12}
        System.Collections.Generic.IEnumerable<System.Type> ctrls = asm.GetTypes().Where(t =>
            t.IsClass &&
            t.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "PacketControllerAttribute"));

        foreach (System.Type ctrl in ctrls)
        {
            // Make HandlerCompiler<ctrl, TPacket> generic method at runtime
            System.Reflection.MethodInfo compiler = typeof(HandlerPrecompiler).GetMethod(nameof(CompileGeneric),
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!.MakeGenericMethod(ctrl, typeof(TPacket));

            System.Object instance = controllerFactory(ctrl);
            System.Array handlers = (System.Array)compiler.Invoke(null, [instance])!;

            created.Add(instance);
        }

        return created;
    }

    private static PacketHandler<TPacket>[] CompileGeneric<TController, TPacket>(System.Object instance)
        where TController : class
        where TPacket : IPacket
    {
        return HandlerCompiler<TController, TPacket>.CompileHandlers(() => (TController)instance);
    }
}
