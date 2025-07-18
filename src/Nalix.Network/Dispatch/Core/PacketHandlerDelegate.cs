namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Enhanced version of PacketHandlerDelegate using compiled delegates for zero-allocation execution.
/// </summary>
/// <typeparam name="TPacket">The packet type handled by this delegate.</typeparam>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
public readonly struct PacketHandlerDelegate<TPacket>(
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
    /// <returns>A task that completes with the handler’s result.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask<System.Object?> ExecuteAsync(PacketContext<TPacket> context)
    {
        return CompiledInvoker(ControllerInstance, context);
    }

    /// <summary>
    /// <para>✅ <b>Extendable Logic</b>:</para>
    /// <para>You can extend this method to implement additional validation rules such as:</para>
    /// <list type="bullet">
    /// <item><description><b>Permission Checks</b>: Ensure the user has sufficient access level.</description></item>
    /// <item><description><b>Rate Limiting</b>: Enforce request intervals to prevent abuse.</description></item>
    /// <item><description><b>Custom Filters</b>: Add any domain-specific condition (e.g., session validity).</description></item>
    /// </list>
    ///
    /// <para>🔧 <b>How to Extend</b>:</para>
    /// <para>Un-comment or implement logic based on <c>PacketContext&lt;TPacket&gt;</c>, for example:</para>
    /// <code>
    /// if (Attributes.Permission != null &amp;&amp; Attributes.Permission.Level &gt; context.Connection.Level)
    ///     return false;
    /// </code>
    ///
    /// <para>Or for rate limiting:</para>
    /// <code>
    /// var key = $"rate_{context.Connection.Id}_{OpCode}";
    /// var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    /// var last = context.GetValueProperty&lt;long&gt;(key);
    /// if (now - last &lt; Attributes.RateLimit.Interval) return false;
    /// context.SetProperty(key, now);
    /// </code>
    ///
    /// <para>🔒 <b>Security Note</b>:</para>
    /// <para>Always call <c>CanExecute()</c> before executing the handler to prevent unauthorized or abusive access.</para>
    ///
    /// </summary>
    /// <param name="_">The current packet context. Pass this to validation logic as needed.</param>
    /// <returns>True if the handler can be executed; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool CanExecute(PacketContext<TPacket> _)
    {
        return true;
    }

    #endregion Methods
}
