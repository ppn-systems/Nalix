// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol : IReportable
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
    /// Generates a diagnostic report on the current state of the protocol.
    /// Includes metrics like connection status, message count, and error count.
    /// </summary>
    /// <returns>A formatted string containing the protocol status report.</returns>
    public virtual System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Protocol Status:");
        _ = sb.AppendLine("--------------------------------------");
        _ = sb.AppendLine($"Is Accepting: {IsAccepting}");
        _ = sb.AppendLine($"Keep Connection Open: {KeepConnectionOpen}");
        _ = sb.AppendLine($"Total Messages: {TotalMessages}");
        _ = sb.AppendLine($"Total Errors: {TotalErrors}");
        _ = sb.AppendLine("--------------------------------------");
        _ = sb.AppendLine();

        return sb.ToString();
    }
}
