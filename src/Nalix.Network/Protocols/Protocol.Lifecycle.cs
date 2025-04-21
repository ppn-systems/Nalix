namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private bool _isDisposed;
    private int _keepConnectionOpen;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether the connection should be kept open after processing.
    /// Standard value is false unless overridden.
    /// Thread-safe implementation using atomic operations.
    /// </summary>
    public virtual bool KeepConnectionOpen
    {
        get => System.Threading.Interlocked.CompareExchange(ref _keepConnectionOpen, 0, 0) == 1;
        protected set => System.Threading.Interlocked.Exchange(ref _keepConnectionOpen, value ? 1 : 0);
    }

    #endregion

    /// <summary>
    /// Disposes resources used by this Protocol.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override this method to clean up any resources when the protocol is disposed.
    /// </summary>
    protected virtual void OnDisposing() { }

    /// <summary>
    /// Disposes resources used by this Protocol.
    /// </summary>
    /// <param name="disposing">True if called explicitly, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        // Dispose of managed resources
        if (disposing) this.OnDisposing();

        _isDisposed = true;
    }
}
