// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>All interactive command handlers for the TUI menu — exercises every SDK extension.</summary>
internal sealed class CommandRunner : IDisposable
{
    private readonly ClientSession _client;
    private readonly StatusBar     _status;
    private readonly EventLog      _log;
    private readonly PingChart     _chart;

    // Background keepalive task
    private CancellationTokenSource? _keepAliveCts;

    // Active subscriptions (On<T>) that can be toggled
    private IDisposable? _controlSub;
    private bool         _controlSubActive;

    public CommandRunner(ClientSession client, StatusBar status, EventLog log, PingChart chart)
    {
        _client = client;
        _status = status;
        _log    = log;
        _chart  = chart;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONNECTION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Connects to the server (plain TCP, no handshake — example server is unencrypted).</summary>
    public async Task ConnectAsync()
    {
        if (_client.IsConnected)
        {
            _log.Warn("Already connected.");
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("aqua"))
            .StartAsync("Connecting...", async ctx =>
            {
                ctx.Status("Establishing TCP connection...");
                try
                {
                    await _client.ConnectAsync().ConfigureAwait(false);
                    _status.SetConnected(true);
                    _log.Success($"Connected to {_client.Session.Options.Address}:{_client.Session.Options.Port}.");

                    _client.Session.OnDisconnected += (_, ex) =>
                    {
                        _status.SetConnected(false);
                        _log.Error($"Disconnected: {ex.Message}");
                        StopKeepAlive();
                    };

                    _client.Session.OnError += (_, ex) =>
                    {
                        _status.IncrementErrors();
                        _log.Error($"Session error: {ex.Message}");
                    };

                    StartKeepAlive();
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Connection failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    /// <summary>Sends a graceful DISCONNECT control frame, then closes the socket. (DisconnectExtensions)</summary>
    public async Task GracefulDisconnectAsync()
    {
        if (!_client.IsConnected) { _log.Warn("Not connected."); return; }

        StopKeepAlive();
        StopControlSubscription();

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

    /// <summary>Hard disconnect without a graceful frame.</summary>
    public async Task HardDisconnectAsync()
    {
        if (!_client.IsConnected) { _log.Warn("Not connected."); return; }
        StopKeepAlive();
        StopControlSubscription();
        await _client.DisconnectAsync().ConfigureAwait(false);
        _status.SetConnected(false);
        _log.Info("Hard disconnect complete.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HANDSHAKE & SECURITY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Performs X25519 ECDH handshake. (HandshakeExtensions)</summary>
    public async Task HandshakeAsync()
    {
        if (!RequireConnected()) return;

        string? pinnedKey = _client.Session.Options.ServerPublicKey;
        if (string.IsNullOrEmpty(pinnedKey))
        {
            _log.Warn("ServerPublicKey is not set in TransportOptions — handshake will fail for security reasons.");
            _log.Info("Tip: Set Options.ServerPublicKey to the server's X25519 public key (hex) to enable handshake.");
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .SpinnerStyle(Style.Parse("mediumpurple1"))
            .StartAsync("Performing X25519 handshake...", async _ =>
            {
                try
                {
                    await _client.Session.HandshakeAsync().ConfigureAwait(false);
                    _log.Success("Handshake complete! Session is now encrypted.");
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Handshake failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    /// <summary>Attempts session resume, falls back to handshake if needed. (ResumeExtensions)</summary>
    public async Task ResumeSessionAsync()
    {
        if (!RequireConnected()) return;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("mediumpurple1"))
            .StartAsync("Attempting session resume...", async _ =>
            {
                try
                {
                    ProtocolReason reason = await _client.Session.ResumeSessionAsync().ConfigureAwait(false);
                    if (reason == ProtocolReason.NONE)
                    {
                        _log.Success("Session resumed successfully! Encryption active.");
                    }
                    else
                    {
                        _log.Warn($"Resume failed: {reason}. You may need to do a full handshake.");
                    }
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Resume error: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    /// <summary>Rotates the cipher suite live. (CipherExtensions)</summary>
    public async Task UpdateCipherAsync()
    {
        if (!RequireConnected()) return;

        CipherSuiteType chosen = AnsiConsole.Prompt(
            new SelectionPrompt<CipherSuiteType>()
                .Title("[steelblue1]Select new cipher suite:[/]")
                .AddChoices(Enum.GetValues<CipherSuiteType>()));

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .SpinnerStyle(Style.Parse("gold1"))
            .StartAsync($"Rotating cipher to {chosen}...", async _ =>
            {
                try
                {
                    await _client.Session.UpdateCipherAsync(chosen).ConfigureAwait(false);
                    _log.Success($"Cipher updated to {chosen}.");
                }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Error($"Cipher update failed: {ex.Message}");
                }
            }).ConfigureAwait(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Sends a single PING and awaits a PONG. (PingExtensions)</summary>
    public async Task PingOnceAsync()
    {
        if (!RequireConnected()) return;
        try
        {
            double ms = await _client.PingOnceAsync().ConfigureAwait(false);
            _status.UpdatePing(ms);
            _chart.AddSample(ms);
            _log.Ping(ms);
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error("Ping timed out (no PONG within 5s).");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"Ping error: {ex.Message}");
        }
    }

    /// <summary>Sends N pings with configurable interval showing a progress bar. (PingExtensions)</summary>
    public async Task ContinuousPingAsync()
    {
        if (!RequireConnected()) return;

        int count = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]How many pings?[/] [grey](1–500)[/]")
                .DefaultValue(10)
                .Validate(n => n is >= 1 and <= 500
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 1–500[/]")));

        int intervalMs = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Interval (ms)?[/] [grey](100–5000)[/]")
                .DefaultValue(500)
                .Validate(n => n is >= 100 and <= 5000
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 100–5000[/]")));

        _log.Info($"Starting {count} pings @ {intervalMs}ms...");

        int ok = 0, fail = 0;
        double total = 0;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(),
                     new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn(Spinner.Known.Dots))
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[aqua]Pinging server[/]", maxValue: count);
                for (int i = 0; i < count && _client.IsConnected; i++)
                {
                    try
                    {
                        double ms = await _client.PingOnceAsync(timeoutMs: 3000).ConfigureAwait(false);
                        _status.UpdatePing(ms); _chart.AddSample(ms); _log.Ping(ms);
                        total += ms; ok++;
                    }
                    catch (TimeoutException) { _status.IncrementErrors(); _log.Error($"#{i + 1} timeout"); fail++; }
                    catch (Exception ex)     { _status.IncrementErrors(); _log.Error($"#{i + 1}: {ex.Message}"); fail++; }

                    task.Increment(1);
                    if (i < count - 1) await Task.Delay(intervalMs).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

        _log.Success(ok > 0
            ? $"Done! {ok}/{count} OK  avg={total / ok:F2}ms  fail={fail}"
            : $"All {count} pings failed.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIME SYNC
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Sends a TIMESYNCREQUEST and adjusts the local clock. (TimeSyncExtensions)</summary>
    public async Task TimeSyncAsync()
    {
        if (!RequireConnected()) return;
        try
        {
            var (rtt, adj) = await _client.Session.SyncTimeAsync().ConfigureAwait(false);
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

    // ═══════════════════════════════════════════════════════════════════════
    // CONTROL FRAMES  (ControlExtensions / SendControlAsync)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a sub-menu to pick any ControlType and fire a SendControlAsync.
    /// (ControlExtensions.SendControlAsync)
    /// </summary>
    public async Task SendControlFrameAsync()
    {
        if (!RequireConnected()) return;

        ControlType type = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Select ControlType to send:[/]")
                .PageSize(12)
                .AddChoices(Enum.GetValues<ControlType>()));

        try
        {
            await _client.Session.SendControlAsync(
                opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
                type: type).ConfigureAwait(false);

            _log.Send("CONTROL", $"type={type} opcode={ProtocolOpCode.SYSTEM_CONTROL}");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"SendControl failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Fires a raw SendControlAsync and then waits for a matching CONTROL response.
    /// (ControlExtensions.AwaitControlAsync)
    /// </summary>
    public async Task AwaitControlFrameAsync()
    {
        if (!RequireConnected()) return;

        ControlType sendType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Send ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        ControlType awaitType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Await response ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        int timeoutMs = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Timeout (ms)?[/]").DefaultValue(5000));

        try
        {
            // Send first
            await _client.Session.SendControlAsync(
                opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
                type: sendType).ConfigureAwait(false);

            _log.Send("CONTROL", $"type={sendType} — awaiting {awaitType}...");

            // Then await the expected response
            using Control response = await _client.Session.AwaitControlAsync(
                predicate: c => c.Type == awaitType,
                timeoutMs: timeoutMs).ConfigureAwait(false);

            _log.Recv("CONTROL", $"type={response.Type} seq={response.SequenceId} reason={response.Reason}");
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error($"No {awaitType} response within {timeoutMs}ms.");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"AwaitControl failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SUBSCRIPTIONS  (TcpSessionSubscriptions)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Toggles a persistent On&lt;Control&gt; subscription that logs every Control frame received.
    /// (TcpSessionSubscriptions.On&lt;T&gt;)
    /// </summary>
    public void ToggleControlSubscription()
    {
        if (_controlSubActive)
        {
            StopControlSubscription();
            _log.Info("Control frame subscription stopped.");
        }
        else
        {
            _controlSub = _client.Session.On<Control>(ctrl =>
            {
                _log.Recv("CONTROL", $"type={ctrl.Type} seq={ctrl.SequenceId} reason={ctrl.Reason} ts={ctrl.Timestamp}");
            });
            _controlSubActive = true;
            _log.Success("Control frame subscription active — all incoming Control frames will be logged.");
        }
    }

    /// <summary>
    /// Registers a OnOnce&lt;Control&gt; one-shot subscription that fires for the next matching frame.
    /// (TcpSessionSubscriptions.OnOnce&lt;T&gt;)
    /// </summary>
    public void RegisterOneShotSubscription()
    {
        if (!RequireConnected()) return;

        ControlType target = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Wait for which ControlType?[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        _ = _client.Session.OnOnce<Control>(
            predicate: c => c.Type == target,
            handler:   c => _log.Recv($"ONE-SHOT [{target}]", $"seq={c.SequenceId} reason={c.Reason}"));

        _log.Info($"One-shot subscription registered — will fire once when {target} arrives.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REQUEST-RESPONSE  (RequestExtensions)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sends a Control frame and awaits a correlated Control response using RequestAsync&lt;T&gt;.
    /// (RequestExtensions.RequestAsync)
    /// </summary>
    public async Task RequestResponseAsync()
    {
        if (!RequireConnected()) return;

        ControlType sendType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Request (send) ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        ControlType expectType = AnsiConsole.Prompt(
            new SelectionPrompt<ControlType>()
                .Title("[steelblue1]Expected response ControlType:[/]")
                .AddChoices(Enum.GetValues<ControlType>()));

        int timeoutMs = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Timeout (ms)?[/]").DefaultValue(5000));

        int retries = AnsiConsole.Prompt(
            new TextPrompt<int>("[steelblue1]Retry count?[/]").DefaultValue(0));

        bool encrypt = AnsiConsole.Confirm("[steelblue1]Encrypt?[/]", defaultValue: false);

        using Control request = _client.Session
            .NewControl((ushort)ProtocolOpCode.SYSTEM_CONTROL, sendType)
            .Build();

        var opts = RequestOptions.Default
            .WithTimeout(timeoutMs)
            .WithRetry(retries);

        if (encrypt) opts = opts.WithEncrypt();

        try
        {
            _log.Send("REQUEST", $"type={sendType}  expect={expectType}  timeout={timeoutMs}ms  retry={retries}  enc={encrypt}");

            using Control response = await _client.Session.RequestAsync<Control>(
                request,
                options: opts,
                predicate: c => c.Type == expectType).ConfigureAwait(false);

            _log.Recv("RESPONSE", $"type={response.Type} seq={response.SequenceId} reason={response.Reason} ts={response.Timestamp}");
        }
        catch (TimeoutException)
        {
            _status.IncrementErrors();
            _log.Error($"RequestAsync<Control> timed out after {timeoutMs}ms × {retries + 1} attempts.");
        }
        catch (Exception ex)
        {
            _status.IncrementErrors();
            _log.Error($"RequestAsync error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHART / MISC
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowChart()
    {
        AnsiConsole.Clear();
        Banner.Render();
        _chart.Render();
        _log.Render(12);
        _status.Render();
        AnsiConsole.MarkupLine("\n[grey]Press any key to return...[/]");
        _ = Console.ReadKey(true);
    }

    public void ShowServerInfo()
    {
        var opts = _client.Session.Options;

        var tbl = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Title("[aqua bold]Session & Transport Options[/]")
            .AddColumn(new TableColumn("[grey]Property[/]").Centered())
            .AddColumn(new TableColumn("[grey]Value[/]").Centered());

        tbl.AddRow("[steelblue1]Address[/]",         $"[white]{opts.Address}[/]");
        tbl.AddRow("[steelblue1]Port[/]",             $"[white]{opts.Port}[/]");
        tbl.AddRow("[steelblue1]Connected[/]",        _client.IsConnected ? "[green]Yes[/]" : "[red]No[/]");
        tbl.AddRow("[steelblue1]Encryption[/]",       opts.EncryptionEnabled ? "[green]Enabled[/]" : "[yellow]Disabled[/]");
        tbl.AddRow("[steelblue1]Algorithm[/]",         $"[mediumpurple1]{opts.Algorithm}[/]");
        tbl.AddRow("[steelblue1]Server PublicKey[/]",  string.IsNullOrEmpty(opts.ServerPublicKey)
            ? "[grey]not set[/]"
            : $"[mediumpurple1]{opts.ServerPublicKey[..Math.Min(16, opts.ServerPublicKey.Length)]}…[/]");
        tbl.AddRow("[steelblue1]Compression[/]",      opts.CompressionEnabled ? "[green]Enabled[/]" : "[grey]Off[/]");
        tbl.AddRow("[steelblue1]Compression Thresh[/]", $"[white]{opts.CompressionThreshold}B[/]");
        tbl.AddRow("[steelblue1]Reconnect[/]",        opts.ReconnectEnabled ? "[green]Yes[/]" : "[grey]No[/]");
        tbl.AddRow("[steelblue1]Reconnect Attempts[/]", $"[white]{opts.ReconnectMaxAttempts}[/]");
        tbl.AddRow("[steelblue1]Connect Timeout[/]",  $"[white]{opts.ConnectTimeoutMillis}ms[/]");
        tbl.AddRow("[steelblue1]Resume Enabled[/]",   opts.ResumeEnabled ? "[green]Yes[/]" : "[grey]No[/]");
        tbl.AddRow("[steelblue1]Resume Timeout[/]",   $"[white]{opts.ResumeTimeoutMillis}ms[/]");
        tbl.AddRow("[steelblue1]Session Token[/]",    $"[white]{opts.SessionToken}[/]");
        tbl.AddRow("[steelblue1]Control Sub[/]",      _controlSubActive ? "[green]Active[/]" : "[grey]Inactive[/]");

        AnsiConsole.Write(tbl);
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        _ = Console.ReadKey(true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BACKGROUND KEEPALIVE
    // ═══════════════════════════════════════════════════════════════════════

    private void StartKeepAlive()
    {
        StopKeepAlive();
        _keepAliveCts = new CancellationTokenSource();
        var ct = _keepAliveCts.Token;

        _ = Task.Run(async () =>
        {
            const int IntervalMs = 15_000;
            _log.Info("Background keepalive started (15s interval, using PingAsync).");

            while (!ct.IsCancellationRequested && _client.IsConnected)
            {
                try
                {
                    await Task.Delay(IntervalMs, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested || !_client.IsConnected) break;

                    double ms = await _client.PingOnceAsync(timeoutMs: 5000, ct).ConfigureAwait(false);
                    _status.UpdatePing(ms);
                    _chart.AddSample(ms);
                    _log.Info($"[Keepalive] ping {ms:F2}ms");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Warn($"[Keepalive] error: {ex.Message}");
                }
            }

            _log.Info("Background keepalive stopped.");
        }, CancellationToken.None);
    }

    private void StopKeepAlive()
    {
        var cts = Interlocked.Exchange(ref _keepAliveCts, null);
        try   { cts?.Cancel(); }
        catch { /* ignore */ }
        finally { cts?.Dispose(); }
    }

    private void StopControlSubscription()
    {
        var sub = Interlocked.Exchange(ref _controlSub, null);
        sub?.Dispose();
        _controlSubActive = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private bool RequireConnected()
    {
        if (_client.IsConnected) return true;
        _log.Error("Not connected — use [Connect] first.");
        return false;
    }

    public void Dispose()
    {
        StopKeepAlive();
        StopControlSubscription();
    }
}
