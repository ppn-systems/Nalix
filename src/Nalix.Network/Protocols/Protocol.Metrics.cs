// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Common.Abstractions;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol : IReportable
{
    #region Fields

    private ulong _totalErrors;
    private ulong _totalMessages;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Total number of errors encountered during message processing.
    /// </summary>
    public ulong TotalErrors => Interlocked.Read(ref _totalErrors);

    /// <summary>
    /// Total number of messages processed by this protocol.
    /// </summary>
    public ulong TotalMessages => Interlocked.Read(ref _totalMessages);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Generates a human-readable report describing the current protocol state.
    /// </summary>
    /// <returns>A formatted string containing the protocol status report.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual string GenerateReport()
    {
        StringBuilder sb = new(128);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Protocol Status:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Is Disposed             : {_isDisposed}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Messages          : {this.TotalMessages}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Errors            : {this.TotalErrors}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Is Accepting            : {this.IsAccepting}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Keep Connections Open   : {this.KeepConnectionOpen}");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates protocol report data as key-value pairs for machine consumption.
    /// </summary>
    /// <returns>A dictionary containing the protocol report data.</returns>
    public virtual IDictionary<string, object> GenerateReportData()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["IsDisposed"] = _isDisposed,
            ["TotalMessages"] = this.TotalMessages,
            ["TotalErrors"] = this.TotalErrors,
            ["IsAccepting"] = this.IsAccepting,
            ["KeepConnectionOpen"] = this.KeepConnectionOpen
        };
    }

    #endregion Public Methods
}
