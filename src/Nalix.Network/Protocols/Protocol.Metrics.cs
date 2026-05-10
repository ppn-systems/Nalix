// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions;

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

    /// <inheritdoc/>
    public virtual void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteBoolean("IsDisposed", _isDisposed != 0);
        writer.WriteNumber(nameof(this.TotalMessages), this.TotalMessages);
        writer.WriteNumber(nameof(this.TotalErrors), this.TotalErrors);
        writer.WriteBoolean(nameof(this.IsAccepting), this.IsAccepting);
        writer.WriteBoolean(nameof(this.KeepConnectionOpen), this.KeepConnectionOpen);
        writer.WriteEndObject();
    }

    #endregion Public Methods
}
