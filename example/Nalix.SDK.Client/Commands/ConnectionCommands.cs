// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.Services;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Transport.Extensions;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>Connect, graceful disconnect, and hard disconnect commands.</summary>
internal sealed class ConnectionCommands
{
    private readonly ClientSession _client;
    private readonly StatusBar _status;
    private readonly EventLog _log;
    private readonly KeepAliveService _keepAlive;
    private readonly SubscriptionManager _subs;

    public ConnectionCommands(ClientSession client, StatusBar status, EventLog log,
                              KeepAliveService keepAlive, SubscriptionManager subs)
    {
        _client = client;
        _status = status;
        _log = log;
        _keepAlive = keepAlive;
        _subs = subs;
    }

    public async Task ConnectAsync()
    {
        if (_client.IsConnected) { _log.Warn("Already connected."); return; }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("aqua"))
            .StartAsync("Connecting...", async ctx =>
            {
                _ = ctx.Status("Establishing TCP connection...");
                try
                {
                    await _client.ConnectAsync().ConfigureAwait(false);
                    _status.SetConnected(true);
                    _log.Success($"Connected to {_client.Session.Options.Address}:{_client.Session.Options.Port}.");

                    _client.Session.OnDisconnected += (_, ex) =>
                    {
                        _status.SetConnected(false);
                        _log.Error($"Disconnected: {ex.Message}");
                        _keepAlive.Stop();
                    };

                    _client.Session.OnError += (_, ex) =>
                    {
                        _status.IncrementErrors();
                        _log.Error($"Session error: {ex.Message}");
                    };

                    _keepAlive.Start();
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Connection failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    public async Task GracefulDisconnectAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        _keepAlive.Stop();
        _subs.StopControlSubscription();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("grey"))
            .StartAsync("Disconnecting gracefully...", async _ =>
            {
                try
                {
                    await _client.Session.DisconnectGracefullyAsync(ProtocolReason.NONE).ConfigureAwait(false);
                    _status.SetConnected(false);
                    _log.Info("Graceful disconnect sent and socket closed.");
                }
                catch (Exception ex)
                {
                    _log.Warn($"Graceful disconnect error (falling back to hard close): {ex.Message}");
                    await _client.DisconnectAsync().ConfigureAwait(false);
                    _status.SetConnected(false);
                }
            }).ConfigureAwait(false);
    }

    public async Task HardDisconnectAsync()
    {
        if (!this.RequireConnected())
        {
            return;
        }

        _keepAlive.Stop();
        _subs.StopControlSubscription();
        await _client.DisconnectAsync().ConfigureAwait(false);
        _status.SetConnected(false);
        _log.Info("Hard disconnect complete.");
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
