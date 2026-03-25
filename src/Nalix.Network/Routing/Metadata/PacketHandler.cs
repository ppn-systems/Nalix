// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing.Metadata;

/// <summary>
/// Enhanced version of <c>PacketHandler</c> using compiled delegates for zero-allocation execution.
/// </summary>
/// <typeparam name="TPacket">The packet type handled by this delegate.</typeparam>
/// <param name="opCode"></param>
/// <param name="metadata"></param>
/// <param name="controllerInstance"></param>
/// <param name="method"></param>
/// <param name="returnType"></param>
/// <param name="compiledInvoker"></param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct PacketHandler<TPacket>(
    ushort opCode, PacketMetadata metadata,
    object controllerInstance, System.Reflection.MethodInfo method, System.Type returnType,
    System.Func<object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<object>> compiledInvoker) where TPacket : IPacket
{
    #region Fields

    /// <summary>
    /// The OpCode associated with this packet handler.
    /// </summary>
    public readonly ushort OpCode = opCode;

    /// <summary>
    /// The return type of the handler method.
    /// </summary>
    public readonly System.Type ReturnType = returnType;

    /// <summary>
    /// Metadata metadata for this handler (e.g., timeout, rate limiting, permissions).
    /// </summary>
    public readonly PacketMetadata Metadata = metadata;

    /// <summary>
    /// The controller instance to invoke the handler on (cached for reuse).
    /// </summary>
    public readonly object Instance = controllerInstance;

    /// <summary>
    /// The original method info, useful for debugging or reflection fallback.
    /// </summary>
    public readonly System.Reflection.MethodInfo MethodInfo = method;

    /// <summary>
    /// A compiled delegate for invoking the handler directly.
    /// PERFORMANCE CRITICAL: avoids reflection and allocations.
    /// </summary>
    public readonly System.Func<object, PacketContext<TPacket>,
                    System.Threading.Tasks.ValueTask<object>> Invoker = compiledInvoker;

    #endregion Fields

    #region Methods

    /// <summary>
    /// Executes the handler using the compiled delegate for maximum performance.
    /// PERFORMANCE CRITICAL: this is a zero-allocation execution path.
    /// </summary>
    /// <param name="context">The packet context containing the request and metadata.</param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask{TResult}"/> that completes with the handler’s result.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Threading.Tasks.ValueTask<object> ExecuteAsync(PacketContext<TPacket> context) => Invoker(Instance, context);

    /// <summary>
    /// Determines whether this handler can be executed for the specified packet context.
    /// </summary>
    /// <param name="_"></param>
    /// <returns><see langword="true"/> if the handler can be executed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method can be extended to implement validation logic such as:
    /// <list type="bullet">
    /// <item><description>Permission checks</description></item>
    /// <item><description>Rate limiting</description></item>
    /// <item><description>Custom filters</description></item>
    /// </list>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public bool CanExecute(PacketContext<TPacket> _) => true;

    #endregion Methods
}
