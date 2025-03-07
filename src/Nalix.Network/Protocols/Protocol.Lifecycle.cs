// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private System.Int32 _isDisposed;
    private System.Int32 _keepConnectionOpen;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether the connection should be kept open after processing.
    /// Standard value is false unless overridden.
    /// Thread-safe implementation using atomic operations.
    /// </summary>
    public virtual System.Boolean KeepConnectionOpen
    {
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.CompareExchange(ref this._keepConnectionOpen, 0, 0) == 1;

        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        protected set => System.Threading.Interlocked.Exchange(ref this._keepConnectionOpen, value ? 1 : 0);
    }

    #endregion Properties

    #region Disposal

    /// <summary>
    /// Disposes resources used by this ProtocolType.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[NW.{nameof(Protocol)}:{nameof(Dispose)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core disposal logic. Override to release managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    protected virtual void Dispose(System.Boolean disposing)
    {
        // Atomic check-and-set: 0 -> 1
        // If already 1, return immediately (already disposed)
        if (System.Threading.Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        {
            return;
        }

        // Optional: clean up managed resources if (disposing)
    }

    #endregion Disposal
}