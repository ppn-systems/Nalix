// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Delegates;

/// <summary>
/// Enhanced version of <c>PacketHandler</c> using compiled delegates for zero-allocation execution.
/// </summary>
/// <typeparam name="TPacket">The packet type handled by this delegate.</typeparam>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
public readonly struct PacketHandler<TPacket>(
    System.UInt16 opCode,
    PacketMetadata attributes,
    System.Object controllerInstance,
    System.Reflection.MethodInfo method,
    System.Type returnType,
    System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> compiledInvoker)
{
    #region Fields

    /// <summary>
    /// The OpCode associated with this packet handler.
    /// </summary>
    public readonly System.UInt16 OpCode = opCode;

    /// <summary>
    /// Metadata attributes for this handler (e.g., timeout, rate limiting, permissions).
    /// </summary>
    public readonly PacketMetadata Attributes = attributes;

    /// <summary>
    /// The controller instance to invoke the handler on (cached for reuse).
    /// </summary>
    public readonly System.Object ControllerInstance = controllerInstance;

    /// <summary>
    /// The original method info, useful for debugging or reflection fallback.
    /// </summary>
    public readonly System.Reflection.MethodInfo Method = method;

    /// <summary>
    /// The return type of the handler method.
    /// </summary>
    public readonly System.Type ReturnType = returnType;

    /// <summary>
    /// A compiled delegate for invoking the handler directly.
    /// PERFORMANCE CRITICAL: avoids reflection and allocations.
    /// </summary>
    public readonly System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker = compiledInvoker;

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask<System.Object?> ExecuteAsync(PacketContext<TPacket> context)
        => this.CompiledInvoker(this.ControllerInstance, context);

    /// <summary>
    /// Determines whether this handler can be executed for the specified packet context.
    /// </summary>
    /// <param name="_">The current packet context. Pass this to validation logic as needed.</param>
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean CanExecute(PacketContext<TPacket> _) => true;

    #endregion Methods
}
