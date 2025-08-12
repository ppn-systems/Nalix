namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private System.UInt64 _totalErrors;
    private System.UInt64 _totalMessages;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Total number of errors encountered during message processing.
    /// </summary>
    public System.UInt64 TotalErrors => System.Threading.Interlocked.Read(ref this._totalErrors);

    /// <summary>
    /// Total number of messages processed by this protocol.
    /// </summary>
    public System.UInt64 TotalMessages => System.Threading.Interlocked.Read(ref this._totalMessages);

    #endregion Properties

    /// <summary>
    /// Represents immutable statistics for a network protocol.
    /// </summary>
    public record ProtocolStats
    {
        /// <summary>
        /// Gets a value indicating whether the protocol is currently accepting connections.
        /// </summary>
        public System.Boolean IsListening { get; init; }

        /// <summary>
        /// Gets the total number of connection errors encountered by the protocol.
        /// </summary>
        public System.UInt64 TotalErrors { get; init; }

        /// <summary>
        /// Gets the total number of messages processed by the protocol.
        /// </summary>
        public System.UInt64 TotalMessages { get; init; }
    }

    /// <summary>
    /// Captures a diagnostic snapshot of the current protocol state,
    /// including connection acceptance status and message statistics.
    /// </summary>
    /// <returns>
    /// A <see cref="ProtocolStats"/> containing metrics like
    /// total messages processed and total errors encountered.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual ProtocolStats GetProtocolStats() => new()
    {
        IsListening = this.IsAccepting,
        TotalErrors = System.Threading.Interlocked.Read(ref this._totalErrors),
        TotalMessages = System.Threading.Interlocked.Read(ref this._totalMessages)
    };
}
