using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Internal.Compilation;
using System.Linq;
using System.Reflection;

namespace Nalix.Network.Registry;

/// <summary>
/// Immutable registry of packet handlers: opcode -> descriptor (metadata + invoker).
/// Built once, then shared read-only for zero-lock lookups.
/// </summary>
/// <typeparam name="TPacket">Packet base type for this registry.</typeparam>
public sealed class PacketMetadataRegistry<TPacket> where TPacket : IPacket
{
    /// <summary>Descriptor for one opcode.</summary>
    public sealed class Descriptor
    {
        /// <summary>
        /// Gets the opcode associated with the packet handler.
        /// </summary>
        public required System.UInt16 OpCode { get; init; }

        /// <inheritdoc/>
        public required PacketMetadata Metadata { get; init; }        // from your codebase

        /// <inheritdoc/>
        public required MethodInfo Method { get; init; }

        /// <inheritdoc/>
        public required System.Type ReturnType { get; init; }

        /// <inheritdoc/>
        public required System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object?>> Invoker { get; init; }
    }

    private readonly System.Collections.Frozen.FrozenDictionary<System.UInt16, Descriptor> _byOpcode;
    private readonly System.Collections.Frozen.FrozenDictionary<System.String, Descriptor[]> _byController; // optional index

    private PacketMetadataRegistry(
        System.Collections.Frozen.FrozenDictionary<System.UInt16, Descriptor> byOpcode,
        System.Collections.Frozen.FrozenDictionary<System.String, Descriptor[]> byController)
    {
        _byOpcode = byOpcode;
        _byController = byController;
    }

    /// <summary>
    /// Try to get descriptor by opcode.
    /// </summary>
    public System.Boolean TryGet(System.UInt16 opcode, out Descriptor? desc) => _byOpcode.TryGetValue(opcode, out desc);

    /// <summary>
    /// List all opcodes.
    /// </summary>
    public System.Collections.Generic.IReadOnlyCollection<System.UInt16> Opcodes => _byOpcode.Keys;

    /// <summary>
    /// List descriptors of a controller (by type name/fullname).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Descriptor> GetByController(System.String controllerName)
        => _byController.TryGetValue(controllerName, out var arr) ? arr : [];

    /// <summary>
    /// Factory: build registry from compiled handlers of multiple controllers.
    /// </summary>
    public static PacketMetadataRegistry<TPacket> Build(
        params System.Collections.Generic.IEnumerable<PacketHandler<TPacket>>[] handlerSets)
    {
        System.Collections.Generic.Dictionary<System.UInt16, Descriptor> dict = new(capacity: 128);
        System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<Descriptor>> byCtrl = new(System.StringComparer.Ordinal);

        foreach (var set in handlerSets.Where(s => s is not null))
        {
            foreach (var h in set!)
            {
                var d = new Descriptor
                {
                    OpCode = h.OpCode,
                    Metadata = h.Metadata,            // PacketMetadata assembled in HandlerCompiler:contentReference[oaicite:9]{index=9}
                    Method = h.MethodInfo,
                    ReturnType = h.ReturnType,
                    Invoker = h.Invoker               // Invoker wrapped as ValueTask<object?>
                };

                // Prefer first come; log duplicates if needed
                dict.TryAdd(h.OpCode, d);

                System.String ctrlName = h.Instance.GetType().FullName ?? h.Instance.GetType().Name;
                if (!byCtrl.TryGetValue(ctrlName, out System.Collections.Generic.List<Descriptor>? list))
                {
                    list = new System.Collections.Generic.List<Descriptor>(8);
                    byCtrl[ctrlName] = list;
                }
                list.Add(d);
            }
        }

        var frozenByOpcode = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);
        var frozenByCtrl = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(
            byCtrl.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), System.StringComparer.Ordinal),
            System.StringComparer.Ordinal);

        return new PacketMetadataRegistry<TPacket>(frozenByOpcode, frozenByCtrl);
    }


    /// <summary>
    /// Builds a <see cref="PacketMetadataRegistry{TPacket}"/> from compiled packet handlers of the specified controller type.
    /// Uses the provided factory to instantiate the controller and compiles its packet handler methods.
    /// </summary>
    /// <typeparam name="TController">The controller type containing packet handler methods.</typeparam>
    /// <param name="factory">A factory function to create an instance of <typeparamref name="TController"/>.</param>
    /// <returns>
    /// An immutable <see cref="PacketMetadataRegistry{TPacket}"/> containing descriptors for all compiled packet handlers
    /// found in the specified controller.
    /// </returns>
    public static PacketMetadataRegistry<TPacket> BuildFromCompiledControllers<TController>(
        System.Func<TController> factory)
        where TController : class
    {
        // Bridge via HandlerCompiler to produce PacketHandler<TPacket>[]:contentReference[oaicite:10]{index=10}
        var handlers = HandlerCompiler<TController, TPacket>.CompileHandlers(factory);
        return Build(handlers);
    }
}
