using Nalix.Common.Connection;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Network.Dispatch.BuiltIn;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Registers default handlers for the packet dispatcher, including controllers for session management,
    /// keep-alive functionality, and mode handling. This method sets up the standard set of handlers
    /// used by the dispatcher using their respective controller types.
    /// </summary>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance, allowing for method chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> BuiltInHandlers()
    {
        this.WithHandler<SessionController<TPacket>>();
        this.WithHandler<KeepAliveController<TPacket>>();
        this.WithHandler(() => new ModeController<TPacket>(_logger));

        return this;
    }

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketIdAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<[DynamicallyAccessedMembers(RequiredMembers)] TController>()
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>
        (TController instance) where TController : class
        => WithHandler(() => EnsureNotNull(instance, nameof(instance)));

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// using a provided factory function, then scanning its methods decorated
    /// with <see cref="PacketIdAttribute"/>.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>
        (System.Func<TController> factory) where TController : class
    {
        System.Type controllerType = typeof(TController);
        PacketControllerAttribute controllerAttr = controllerType.GetCustomAttribute<PacketControllerAttribute>()
            ?? throw new System.InvalidOperationException(
                $"SessionController '{controllerType.Name}' missing PacketController attribute.");

        string controllerName = controllerAttr.Name ?? controllerType.Name;

        TController controllerInstance = EnsureNotNull(factory(), nameof(factory));

        // Log method scanning process
        _logger?.Debug("Scanning '{0}' for packet handler methods...", controllerName);

        List<MethodInfo> methods = [.. typeof(TController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PacketIdAttribute>() != null)];

        if (methods.Count == 0)
        {
            string message = $"SessionController '{controllerType.FullName}' has no methods marked with [PacketId]. " +
                             $"Ensure at least one public method is decorated.";

            _logger?.Warn(message);
            throw new System.InvalidOperationException(message);
        }

        IEnumerable<ushort> duplicateCommandIds = methods
            .GroupBy(m => m.GetCustomAttribute<PacketIdAttribute>()!.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateCommandIds.Any())
        {
            string message = $"Duplicate PacketId values found in controller " +
                             $"'{controllerName}': {string.Join(", ", duplicateCommandIds)}. " +
                             $"Each handler must have a unique ID.";

            _logger?.Error(message);
            throw new System.InvalidOperationException(message);
        }

        List<ushort> registeredIds = [];

        foreach (MethodInfo method in methods)
        {
            ushort id = method.GetCustomAttribute<PacketIdAttribute>()!.Id;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolveHandler(
        ushort id,
        [NotNullWhen(true)] out System.Func<TPacket, IConnection, Task>? handler)
    {
        if (_handlers.TryGetValue(id, out handler))
            return true;

        Logger?.Warn("No handler found for packet [ID={0}]", id);
        return false;
    }
}
