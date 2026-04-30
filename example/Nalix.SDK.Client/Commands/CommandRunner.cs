// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.Services;
using Nalix.SDK.Client.UI;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>
/// Thin facade that delegates to domain-specific command classes.
/// Composes all dependencies and exposes a single surface for the menu loop.
/// </summary>
internal sealed class CommandRunner : IDisposable
{
    private readonly ConnectionCommands  _conn;
    private readonly SecurityCommands    _security;
    private readonly PingCommands        _ping;
    private readonly DiagnosticCommands  _diag;
    private readonly ControlCommands     _control;
    private readonly ViewCommands        _view;
    private readonly KeepAliveService    _keepAlive;
    private readonly SubscriptionManager _subs;

    public CommandRunner(ClientSession client, StatusBar status, EventLog log, PingChart chart)
    {
        _keepAlive = new KeepAliveService(client, status, log, chart);
        _subs      = new SubscriptionManager(client, log);
        _conn      = new ConnectionCommands(client, status, log, _keepAlive, _subs);
        _security  = new SecurityCommands(client, status, log);
        _ping      = new PingCommands(client, status, log, chart);
        _diag      = new DiagnosticCommands(client, status, log);
        _control   = new ControlCommands(client, status, log);
        _view      = new ViewCommands(client, log, chart, status, _subs);
    }

    // ── Connection ───────────────────────────────────────────────────────────
    public Task ConnectAsync()              => _conn.ConnectAsync();
    public Task GracefulDisconnectAsync()   => _conn.GracefulDisconnectAsync();
    public Task HardDisconnectAsync()       => _conn.HardDisconnectAsync();

    // ── Security ─────────────────────────────────────────────────────────────
    public Task HandshakeAsync()            => _security.HandshakeAsync();
    public Task ResumeSessionAsync()        => _security.ResumeSessionAsync();
    public Task UpdateCipherAsync()         => _security.UpdateCipherAsync();

    // ── Ping ─────────────────────────────────────────────────────────────────
    public Task PingOnceAsync()             => _ping.PingOnceAsync();
    public Task ContinuousPingAsync()       => _ping.ContinuousPingAsync();

    // ── Diagnostics ──────────────────────────────────────────────────────────
    public Task TimeSyncAsync()             => _diag.TimeSyncAsync();

    // ── Control Frames ───────────────────────────────────────────────────────
    public Task SendControlFrameAsync()     => _control.SendControlFrameAsync();
    public Task AwaitControlFrameAsync()    => _control.AwaitControlFrameAsync();
    public Task RequestResponseAsync()      => _control.RequestResponseAsync();

    // ── Subscriptions ────────────────────────────────────────────────────────
    public void ToggleControlSubscription() => _subs.ToggleControlSubscription();

    public void RegisterOneShotSubscription()
    {
        ControlType target = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Wait for which ControlType?[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        _subs.RegisterOneShotSubscription(target);
    }

    // ── View ─────────────────────────────────────────────────────────────────
    public void ShowChart()                 => _view.ShowChart();
    public void ShowServerInfo()            => _view.ShowServerInfo();

    public void Dispose()
    {
        _keepAlive.Dispose();
        _subs.Dispose();
    }
}
