using Nalix.Common.Logging;
using Nalix.Common.Package.Attributes;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// High-performance controller scanner với caching và zero-allocation lookups.
/// Sử dụng compiled expressions cho maximum performance.
/// </summary>
/// <typeparam name="TController">Controller type</typeparam>
/// <typeparam name="TPacket">Packet type</typeparam>
public sealed class PacketAnalyzer<[
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>(ILogger? logger = null)
    where TController : class
    where TPacket : Common.Package.IPacket,
                   Common.Package.IPacketFactory<TPacket>,
                   Common.Package.IPacketEncryptor<TPacket>,
                   Common.Package.IPacketCompressor<TPacket>
{
    #region Fields

    // Cache compiled method accessors để tránh reflection overhead
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, System.Collections.Frozen.FrozenDictionary<
            System.UInt16, CompiledMethodInfo<TPacket>>> _compiledMethodCache = new();

    // Cache attribute lookups
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Reflection.MethodInfo, PacketMetadata> _attributeCache = new();

    #endregion Fields

    /// <summary>
    /// Scan controller và return array của handler descriptors.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketHandlerDelegate<TPacket>[] ScanController(System.Func<TController> factory)
    {
        System.Type controllerType = typeof(TController);


        // Validate controller có PacketController attribute
        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' thiếu [PacketController] attribute.");

        // Get hoặc compile method accessors
        var compiledMethods = GetOrCompileMethodAccessors(controllerType);

        // Create controller instance
        TController controllerInstance = factory();

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Collections.Frozen.FrozenDictionary<
        System.UInt16, CompiledMethodInfo<TPacket>> GetOrCompileMethodAccessors(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] System.Type controllerType)
    {
        var methodInfos = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(
                controllerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
                m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null
            )
        );

        // Fail early nếu không có method nào có PacketOpcode
        if (methodInfos.Length == 0)
        {
            throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' không có method nào với [PacketOpcode] attribute.");
        }

        return _compiledMethodCache.GetOrAdd(controllerType, static (_, methods) =>
        {
            System.Collections.Generic.Dictionary<ushort, CompiledMethodInfo<TPacket>> compiled = new(methods.Length);

            foreach (var method in methods)
            {
                PacketOpcodeAttribute opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                var compiledMethod = CompileMethodAccessor(method);

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    throw new System.InvalidOperationException(
                        $"Duplicate OpCode {opcodeAttr.OpCode} trong controller {method.DeclaringType?.Name ?? "Unknown"}.");
                }

                compiled[opcodeAttr.OpCode] = compiledMethod;
            }

            return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos); // Pass methods as state để không lặp lại logic trong Add
    }

    /// <summary>
    /// Compile method accessor sử dụng expressions cho maximum performance.
    /// PERFORMANCE CRITICAL: Đây là nơi tạo compiled delegates.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private static CompiledMethodInfo<TPacket> CompileMethodAccessor(System.Reflection.MethodInfo method)
    {
        // Tạo expression tree cho method call
        var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");
        var contextParam = System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        // Cast instance to controller type
        var castedInstance = System.Linq.Expressions.Expression.Convert(instanceParam, method.DeclaringType!);

        // Replace the problematic line with the following code to avoid using the Expression.Property method with a string parameter.
        var packetProperty = System.Linq.Expressions.Expression.Property(
            contextParam,
            typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<TPacket>.Packet))!
        );
        var connectionProperty = System.Linq.Expressions.Expression.Property(
            contextParam,
            typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<TPacket>.Connection))!
        );

        // Create method call expression
        var methodCall = System.Linq.Expressions.Expression.Call(
            castedInstance,
            method,
            packetProperty,
            connectionProperty);

        // Handle return type
        System.Linq.Expressions.Expression body;
        if (method.ReturnType == typeof(void))
        {
            // Void method - wrap trong block và return null
            body = System.Linq.Expressions.Expression.Block(
                methodCall,
                System.Linq.Expressions.Expression.Constant(null, typeof(System.Object)));
        }
        else
        {
            // Non-void method - cast result to object
            body = System.Linq.Expressions.Expression.Convert(methodCall, typeof(System.Object));
        }

        // Compile expression thành delegate
        var lambda = System.Linq.Expressions.Expression.Lambda<
            System.Func<System.Object, PacketContext<TPacket>, System.Object?>>(
            body, instanceParam, contextParam);

        var compiledDelegate = lambda.Compile();

        // Wrap trong async delegate nếu cần
        var asyncDelegate = CreateAsyncWrapper(compiledDelegate, method.ReturnType);

        return new CompiledMethodInfo<TPacket>(method, method.ReturnType, asyncDelegate);
    }

    /// <summary>
    /// Tạo async wrapper cho compiled delegate.
    /// </summary>
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object?>>
        CreateAsyncWrapper(
            System.Func<System.Object, PacketContext<TPacket>, System.Object?> syncDelegate,
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] System.Type returnType)
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
                    // Get Result property value
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

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>))
        {
            return async (instance, context) =>
            {
                var result = syncDelegate(instance, context);
                if (result is System.Threading.Tasks.ValueTask valueTask)
                {
                    // This is tricky - need to handle ValueTask<T> generically
                    await valueTask;

                    // Use reflection to get Result (cached for performance)
                    var resultProperty = GetResultProperty(returnType);
                    return resultProperty?.GetValue(result);
                }
                return result;
            };
        }

        // Synchronous method
        return (instance, context) =>
        {
            var result = syncDelegate(instance, context);
            return System.Threading.Tasks.ValueTask.FromResult(result);
        };
    }

    /// <summary>
    /// Get cached attributes cho method.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Reflection.PropertyInfo? GetResultProperty(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] System.Type type)
    {
        return type.GetProperty("Result");
    }
}

/// <summary>
/// Chứa compiled method information để tránh reflection overhead.
/// </summary>
public readonly record struct CompiledMethodInfo<TPacket>(
    System.Reflection.MethodInfo MethodInfo,
    System.Type ReturnType,
    System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker);