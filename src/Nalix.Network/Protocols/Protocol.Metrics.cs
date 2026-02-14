// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Abstractions;

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

    #region Public Methods

    /// <summary>
    /// Generates a diagnostic report on the current state of the protocol.
    /// Includes metrics like connection status, message count, and error count.
    /// </summary>
    /// <returns>A formatted string containing the protocol status report.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public virtual System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Protocol Status:");
        _ = sb.AppendLine("--------------------------------------");
        _ = sb.AppendLine($"Is Accepting: {IsAccepting}");
        _ = sb.AppendLine($"Keep Connections Open: {KeepConnectionOpen}");
        _ = sb.AppendLine($"Total Messages: {TotalMessages}");
        _ = sb.AppendLine($"Total Errors: {TotalErrors}");
        _ = sb.AppendLine("--------------------------------------");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    #endregion Public Methods
}
