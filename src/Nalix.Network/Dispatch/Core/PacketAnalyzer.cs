using Nalix.Common.Logging;
using Nalix.Common.Package.Attributes;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// High-performance controller scanner với caching và zero-allocation lookups.
/// Sử dụng compiled expressions cho maximum performance.
/// </summary>
/// <typeparam name="TController">Controller type</typeparam>
/// <typeparam name="TPacket">Packet type</typeparam>
public sealed class PacketAnalyzer<[
    DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>(ILogger? logger = null)
    where TController : class
    where TPacket : Common.Package.IPacket,
                   Common.Package.IPacketFactory<TPacket>,
                   Common.Package.IPacketEncryptor<TPacket>,
                   Common.Package.IPacketCompressor<TPacket>
{
    #region Fields

    // Cache compiled method accessors để tránh reflection overhead
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Type, FrozenDictionary<System.UInt16, CompiledMethodInfo<TPacket>>> _compiledMethodCache = new();

    // Cache attribute lookups
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        MethodInfo, PacketMetadata> _attributeCache = new();

    #endregion Fields

    /// <summary>
    /// Scan controller và return array của handler descriptors.
    /// Sử dụng compiled expressions cho performance tối ưu.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketHandlerDelegate<TPacket>[] ScanController(System.Func<TController> factory)
    {
        var controllerType = typeof(TController);

        // Validate controller có PacketController attribute
        var controllerAttr = controllerType.GetCustomAttribute<PacketControllerAttribute>() ?? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' thiếu [PacketController] attribute.");

        // Get hoặc compile method accessors
        var compiledMethods = GetOrCompileMethodAccessors(controllerType);

        // Create controller instance
        var controllerInstance = factory();

        var descriptors = new PacketHandlerDelegate<TPacket>[compiledMethods.Count];
        var index = 0;

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

            logger?.Debug("Đã scan handler OpCode={0} Method={1}",
                opCode, compiledMethod.MethodInfo.Name);
        }

        return descriptors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FrozenDictionary<ushort, CompiledMethodInfo<TPacket>> GetOrCompileMethodAccessors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] System.Type controllerType)
    {
        var methodInfos = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<PacketOpcodeAttribute>() is not null)
            .ToArray();

        // Fail early nếu không có method nào có PacketOpcode
        if (methodInfos.Length == 0)
        {
            throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' không có method nào với [PacketOpcode] attribute.");
        }

        return _compiledMethodCache.GetOrAdd(controllerType, static (_, methods) =>
        {
            var compiled = new Dictionary<ushort, CompiledMethodInfo<TPacket>>(methods.Length);

            foreach (var method in methods)
            {
                var opcodeAttr = method.GetCustomAttribute<PacketOpcodeAttribute>()!;
                var compiledMethod = CompileMethodAccessor(method);

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    throw new System.InvalidOperationException(
                        $"Duplicate OpCode {opcodeAttr.OpCode} trong controller {method.DeclaringType?.Name ?? "Unknown"}.");
                }

                compiled[opcodeAttr.OpCode] = compiledMethod;
            }

            return compiled.ToFrozenDictionary();
        }, methodInfos); // Pass methods as state để không lặp lại logic trong Add
    }

    /// <summary>
    /// Compile method accessor sử dụng expressions cho maximum performance.
    /// PERFORMANCE CRITICAL: Đây là nơi tạo compiled delegates.
    /// </summary>
    [SuppressMessage("Trimming",
        "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "<Pending>")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private static CompiledMethodInfo<TPacket> CompileMethodAccessor(MethodInfo method)
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
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] System.Type returnType)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PacketMetadata GetCachedAttributes(MethodInfo method)
    {
        return _attributeCache.GetOrAdd(method, static m => new PacketMetadata(
            m.GetCustomAttribute<PacketOpcodeAttribute>()!,
            m.GetCustomAttribute<PacketTimeoutAttribute>(),
            m.GetCustomAttribute<PacketRateLimitAttribute>(),
            m.GetCustomAttribute<PacketPermissionAttribute>(),
            m.GetCustomAttribute<PacketEncryptionAttribute>()
        ));
    }

    private static PropertyInfo? GetResultProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] System.Type type)
    {
        return type.GetProperty("Result");
    }
}

/// <summary>
/// Chứa compiled method information để tránh reflection overhead.
/// </summary>
public readonly record struct CompiledMethodInfo<TPacket>(
    MethodInfo MethodInfo,
    System.Type ReturnType,
    System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker);