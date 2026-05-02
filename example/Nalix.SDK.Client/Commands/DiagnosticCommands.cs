// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Client.Commands;

/// <summary>Time sync command.</summary>
internal sealed class DiagnosticCommands
{
    private readonly ClientSession _client;
    private readonly StatusBar _status;
    private readonly EventLog _log;

    public DiagnosticCommands(ClientSession client, StatusBar status, EventLog log)
    {
        _client = client;
        _status = status;
        _log = log;
    }

    public async Task TimeSyncAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        try
        {
            (double rtt, double adj) = await _client.Session.SyncTimeAsync().ConfigureAwait(false);
            _log.Success($"Time sync: RTT={rtt:F2}ms  ClockAdjust={adj:F2}ms");
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error("Time sync timed out — server may not support TIMESYNC or encryption mismatch.");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"Time sync error: {ex.Message}");
        }
    }

    private bool RequireConnected()
    {
        if (_client.IsConnected)
        {
            return true;
        }

        _log.Error("Not connected — use [Connect] first.");
        return false;
    }
}
