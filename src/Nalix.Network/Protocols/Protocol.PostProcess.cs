namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    /// <summary>
    /// Allows subclasses to execute custom logic after a message has been processed.
    /// This method is called automatically by <see cref="PostProcessMessage"/>.
    /// </summary>
    /// <param name="args">Event arguments containing connection and processing details.</param>
    protected virtual void OnPostProcess(Common.Connection.IConnectEventArgs args) { }

    /// <summary>
    /// Post-processes a message after it has been handled.
    /// If the connection should not remain open, it will be disconnected.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and additional data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when args is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this protocol instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PostProcessMessage(object sender, Common.Connection.IConnectEventArgs args)
    {
        System.ArgumentNullException.ThrowIfNull(args);
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);
        System.Threading.Interlocked.Increment(ref _totalMessages);

        try
        {
            this.OnPostProcess(args);

            if (!KeepConnectionOpen)
                args.Connection.Disconnect();
        }
        catch (System.Exception ex)
        {
            this.OnConnectionError(args.Connection, ex);
            args.Connection.Disconnect();
        }
    }
}
