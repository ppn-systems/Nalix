namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private System.Boolean _isDisposed;
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
        get => System.Threading.Interlocked.CompareExchange(ref this._keepConnectionOpen, 0, 0) == 1;
        protected set => System.Threading.Interlocked.Exchange(ref this._keepConnectionOpen, value ? 1 : 0);
    }

    #endregion Properties

    /// <summary>
    /// Disposes resources used by this Protocol.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core disposal logic. Override to release managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(System.Boolean disposing)
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;

        // Optional: clean up managed resources if (disposing)
    }
}