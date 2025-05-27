using Nalix.Common.Connection;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    #region Constants

    private const System.Reflection.BindingFlags BindingFlags =
                  System.Reflection.BindingFlags.Public |
                  System.Reflection.BindingFlags.Instance |
                  System.Reflection.BindingFlags.Static;

    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PublicMethods =
                  System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods;

    #endregion Constants

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(RequiredMembers)] TController>()
        where TController : class, new()
        => WithHandler(() => new TController());

    /// <summary>
    /// Registers a handler using an existing instance of the specified controller type.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// </typeparam>
    /// <param name="instance">
    /// An existing instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="instance"/> is null.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PublicMethods)] TController>
        (TController instance) where TController : class
        => WithHandler(() => EnsureNotNull(instance, nameof(instance)));

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// using a provided factory function, then scanning its methods decorated
    /// with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. This type does not require
    /// a parameterless constructor.
    /// </typeparam>
    /// <param name="factory">
    /// A function that returns an instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PublicMethods)] TController>
        (System.Func<TController> factory) where TController : class
    {
        System.Type controllerType = typeof(TController);
        PacketControllerAttribute controllerAttr =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"ConnectionOps '{controllerType.Name}' is missing the PacketController attribute.");

        string controllerName = controllerAttr.Name ?? controllerType.Name;

        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        // Log method scanning process
        _logger?.Debug("Scanning '{0}' for packet handler methods...", controllerName);

        System.Collections.Generic.List<System.Reflection.MethodInfo> methods = [.. System.Linq.Enumerable
            .Where(typeof(TController)
            .GetMethods(BindingFlags), m => System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketOpcodeAttribute>(m) != null)
        ];

        if (methods.Count == 0)
        {
            string message = $"ConnectionOps '{controllerType.FullName}' has no methods marked with [PacketId]. " +
                             $"Ensure at least one public method is decorated.";

            _logger?.Warn(message);
            throw new System.InvalidOperationException(message);
        }

        System.Collections.Generic.IEnumerable<ushort> duplicateCommandIds =
            System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Where(
                    System.Linq.Enumerable.GroupBy(
                        methods,
                        m => System.Reflection.CustomAttributeExtensions
                                .GetCustomAttribute<PacketOpcodeAttribute>(m)!.Opcode
                    ),
                    g => System.Linq.Enumerable.Count(g) > 1
                ),
                g => g.Key
            );

        if (System.Linq.Enumerable.Any(duplicateCommandIds))
        {
            string message = $"Duplicate PacketId values found in controller " +
                             $"'{controllerName}': {string.Join(", ", duplicateCommandIds)}. " +
                             $"Each handler must have a unique ID.";

            _logger?.Error(message);
            throw new System.InvalidOperationException(message);
        }

        System.Collections.Generic.List<ushort> registeredIds = [];

        foreach (System.Reflection.MethodInfo method in methods)
        {
            ushort id = System.Reflection.CustomAttributeExtensions
                        .GetCustomAttribute<PacketOpcodeAttribute>(method)!
                        .Opcode;

            if (_handlers.ContainsKey(id))
            {
                _logger?.Error("PacketId '{0}' already registered in another controller. Conflict in controller '{1}'.",
                                id, controllerName);

                throw new System.InvalidOperationException($"PacketId '{id}' already registered.");
            }

            _handlers[id] = this.CreateHandlerDelegate(method, controllerInstance);
            registeredIds.Add(id);
        }

        _logger?.Info("Registered {0} packet handlers in controller '{1}': [{2}]",
                       registeredIds.Count, controllerName, string.Join(", ", registeredIds));

        return this;
    }

    /// <summary>
    /// Attempts to retrieve a registered packet handler for the specified command Number.
    /// </summary>
    /// <param name="id">The unique identifier of the packet command.</param>
    /// <param name="handler">
    /// When this method returns, contains the handler function associated with the command Number,
    /// or <see langword="null"/> if no handler was found.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a handler for the given <paramref name="id"/> was found;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method looks up the provided command Number in the Internal dictionary of registered packet handlers.
    /// If a matching handler is found, it is returned via the <paramref name="handler"/> output parameter.
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to retrieve and invoke a packet handler:
    /// <code>
    /// if (options.TryResolveHandler(id, out var handler))
    /// {
    ///     await handler(packet, connection);
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No handler found.");
    /// }
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryResolveHandler(
        ushort id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.Func<TPacket, IConnection, System.Threading.Tasks.Task>? handler)
    {
        if (_handlers.TryGetValue(id, out handler))
            return true;

        Logger?.Warn("No handler found for packet [ID={0}]", id);
        return false;
    }
}
