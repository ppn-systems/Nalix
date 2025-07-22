using Nalix.Common.Abstractions;
using Nalix.Network.Observability;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol : ISnapshot<ProtocolStats>
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
    /// Captures a diagnostic snapshot of the current protocol state,
    /// including connection acceptance status and message statistics.
    /// </summary>
    /// <returns>
    /// A <see cref="ProtocolStats"/> containing metrics like
    /// total messages processed and total errors encountered.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual ProtocolStats GetSnapshot() => new()
    {
        IsListening = this.IsAccepting,
        TotalErrors = System.Threading.Interlocked.Read(ref this._totalErrors),
        TotalMessages = System.Threading.Interlocked.Read(ref this._totalMessages)
    };
}
