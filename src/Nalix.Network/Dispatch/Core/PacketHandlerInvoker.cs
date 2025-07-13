namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Enhanced PacketHandlerInvoker với compiled delegates cho zero-allocation execution.
/// </summary>
/// <typeparam name="TPacket">Packet type</typeparam>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
public readonly struct PacketHandlerInvoker<TPacket>(
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
    /// OpCode của packet handler.
    /// </summary>
    public readonly System.UInt16 OpCode = opCode;

    /// <summary>
    /// Attributes của handler (timeout, rate limit, etc.)
    /// </summary>
    public readonly PacketMetadata Attributes = attributes;

    /// <summary>
    /// Controller instance (cached cho reuse)
    /// </summary>
    public readonly System.Object ControllerInstance = controllerInstance;

    /// <summary>
    /// Method info cho debugging/reflection fallback
    /// </summary>
    public readonly System.Reflection.MethodInfo Method = method;

    /// <summary>
    /// Return type của method
    /// </summary>
    public readonly System.Type ReturnType = returnType;

    /// <summary>
    /// Compiled delegate cho direct method invocation (PERFORMANCE CRITICAL)
    /// </summary>
    public readonly System.Func<System.Object, PacketContext<TPacket>,
        System.Threading.Tasks.ValueTask<System.Object?>> CompiledInvoker = compiledInvoker;

    #endregion Fields

    #region Methods

    /// <summary>
    /// Execute handler với maximum performance sử dụng compiled delegate.
    /// PERFORMANCE CRITICAL: Zero allocation execution path.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask<System.Object?> ExecuteAsync(PacketContext<TPacket> context)
    {
        // Direct call thông qua compiled delegate - fastest path
        return CompiledInvoker(ControllerInstance, context);
    }

    /// <summary>
    /// Validate handler có thể execute với context hiện tại.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool CanExecute(PacketContext<TPacket> _)
    {
        // Permission check
        //if (Attributes.Permission is not null &&
        //    Attributes.Permission.Level > context.Connection.Level)
        //{
        //    return false;
        //}

        // Rate limit check (nếu có rate limiter)
        //if (Attributes.RateLimit is not null)
        //{
        //    //var rateLimitKey = $"rate_limit_{context.Connection.Id}_{OpCode}";
        //    //var lastRequest = context.GetValueProperty<System.Int64>(rateLimitKey);
        //    //var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        //    //if (now - lastRequest < Attributes.RateLimit.Level)
        //    //{
        //    //    return false;
        //    //}

        //    //context.SetProperty(rateLimitKey, now);
        //}

        return true;
    }

    #endregion Methods
}