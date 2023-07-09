using Nalix.Common.Logging;
using Nalix.Common.Package.Attributes;
using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Internal.Analyzers;

/// <summary>
/// High-performance controller scanner with caching and zero-allocation lookups.
/// Uses compiled expression trees for maximum dispatch performance.
/// </summary>
/// <typeparam name="TController">The controller type to scan.</typeparam>
/// <typeparam name="TPacket">The packet type handled by this controller.</typeparam>
internal sealed class PacketAnalyzer<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>(ILogger? logger = null)
    where TController : class
    where TPacket : Common.Package.IPacket,
                   Common.Package.IPacketFactory<TPacket>,
                   Common.Package.IPacketEncryptor<TPacket>,
                   Common.Package.IPacketCompressor<TPacket>
{
    #region Fields

    /// <summary>
    /// Caches compiled method delegates for each controller type to eliminate reflection.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Collections.Frozen.FrozenDictionary<
            System.UInt16, CompiledHandler<TPacket>>> _compiledMethodCache = new();

    /// <summary>
    /// Caches attribute lookups per method for performance.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Reflection.MethodInfo, PacketMetadata> _attributeCache = new();

    #endregion

    /// <summary>
    /// Scans the controller and returns an array of packet handler delegates.
    /// </summary>
    /// <param name="factory">A factory method that creates a controller instance.</param>
    /// <returns>An array of compiled packet handler delegates.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketHandlerDelegate<TPacket>[] ScanController(System.Func<TController> factory)
    {
        var controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        var controllerAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        // Get or compile all handler methods
        var compiledMethods = GetOrCompileMethodAccessors(controllerType);

        // Create the controller instance
        TController controllerInstance = factory();

        // Build delegate descriptors
        PacketHandlerDelegate<TPacket>[] descriptors = new PacketHandlerDelegate<TPacket>[compiledMethods.Count];
        System.Int16 index = 0;

        foreach (var (opCode, compiledMethod) in compiledMethods)
        {
            var attributes = GetCachedAttributes(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandlerDelegate<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker);

            logger?.Debug("Scanned handler OpCode={0} Method={1}",
                opCode, compiledMethod.MethodInfo.Name);
        }

        return descriptors;
    }

    /// <summary>
    /// Gets or compiles method handlers for a controller type.
    /// </summary>
    /// <param name="controllerType">The controller type.</param>
    /// <returns>A frozen dictionary of compiled handler delegates indexed by opcode.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Collections.Frozen.FrozenDictionary<System.UInt16, CompiledHandler<TPacket>>
        GetOrCompileMethodAccessors(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)]
        System.Type controllerType)
    {
        // Get methods with [PacketOpcode] attribute
        var methodInfos = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(
                controllerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
                m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null
            ));

        return methodInfos.Length == 0
            ? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' does not define any methods with [PacketOpcode] attribute.")
            : _compiledMethodCache.GetOrAdd(controllerType, static (_, methods) =>
        {
            var compiled = new System.Collections.Generic.Dictionary<System.UInt16, CompiledHandler<TPacket>>(methods.Length);

            foreach (var method in methods)
            {
                var opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                var compiledMethod = CompileMethodAccessor(method);

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    throw new System.InvalidOperationException(
                        $"Duplicate OpCode {opcodeAttr.OpCode} in controller {method.DeclaringType?.Name ?? "Unknown"}.");
                }

                compiled[opcodeAttr.OpCode] = compiledMethod;
            }

            return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    /// <summary>
    /// Compiles a method accessor into a high-performance delegate using expression trees.
    /// </summary>
    /// <param name="method">The method to compile.</param>
    /// <returns>A compiled handler delegate.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The return value of the source method does not have matching annotations.", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private static CompiledHandler<TPacket> CompileMethodAccessor(System.Reflection.MethodInfo method)
    {
        var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");
        var contextParam = System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        var castedInstance = System.Linq.Expressions.Expression.Convert(instanceParam, method.DeclaringType!);

        var packetProperty = System.Linq.Expressions.Expression.Property(
            contextParam,
            typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<TPacket>.Packet))!);

        var connectionProperty = System.Linq.Expressions.Expression.Property(
            contextParam,
            typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<TPacket>.Connection))!);

        var methodCall = System.Linq.Expressions.Expression.Call(
            castedInstance, method, packetProperty, connectionProperty);

        // Wrap result appropriately
        System.Linq.Expressions.Expression body = method.ReturnType == typeof(void)
            ? System.Linq.Expressions.Expression.Block(
                methodCall,
                System.Linq.Expressions.Expression.Constant(null, typeof(global::System.Object)))
            : System.Linq.Expressions.Expression.Convert(methodCall, typeof(global::System.Object));

        var lambda = System.Linq.Expressions.Expression.Lambda<
            System.Func<System.Object, PacketContext<TPacket>, System.Object?>>(
            body, instanceParam, contextParam);

        var compiledDelegate = lambda.Compile();

        var asyncDelegate = CreateAsyncWrapper(compiledDelegate, method.ReturnType);

        return new CompiledHandler<TPacket>(method, method.ReturnType, asyncDelegate);
    }

    /// <summary>
    /// Creates an async-compatible wrapper for a compiled delegate,
    /// handling Task, ValueTask, and their generic variants.
    /// </summary>
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object?>>
        CreateAsyncWrapper(
        System.Func<System.Object, PacketContext<TPacket>, System.Object?> syncDelegate,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        System.Type returnType)
    {
        if (returnType == typeof(System.Threading.Tasks.Task))
        {
            return async (instance, context) =>
            {
                var result = syncDelegate(instance, context);
                if (result is System.Threading.Tasks.Task task)
                {
                    await task;
                    return null;
                }
                return result;
            };
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
        {
            return async (instance, context) =>
            {
                var result = syncDelegate(instance, context);
                if (result is System.Threading.Tasks.Task task)
                {
                    await task;
                    var resultProperty = GetResultProperty(returnType);
                    return resultProperty?.GetValue(task);
                }
                return result;
            };
        }

        if (returnType == typeof(System.Threading.Tasks.ValueTask))
        {
            return async (instance, context) =>
            {
                var result = syncDelegate(instance, context);
                if (result is System.Threading.Tasks.ValueTask valueTask)
                {
                    await valueTask;
                    return null;
                }
                return result;
            };
        }

        return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>)
            ? (async (instance, context) =>
            {
                var result = syncDelegate(instance, context);
                if (result is System.Threading.Tasks.ValueTask valueTask)
                {
                    await valueTask;
                    var resultProperty = GetResultProperty(returnType);
                    return resultProperty?.GetValue(result);
                }
                return result;
            })
            : ((instance, context) => System.Threading.Tasks.ValueTask.FromResult(syncDelegate(instance, context)));
    }

    /// <summary>
    /// Retrieves all cached metadata attributes associated with a handler method.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <returns>The parsed packet metadata.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static PacketMetadata GetCachedAttributes(System.Reflection.MethodInfo method)
    {
        return _attributeCache.GetOrAdd(method, static m => new PacketMetadata(
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m)!,
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m)
        ));
    }

    /// <summary>
    /// Gets the 'Result' property info from a Task/ValueTask generic return type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Reflection.PropertyInfo? GetResultProperty(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        System.Type type) => type.GetProperty("Result");
}
