// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Client.Commands;
using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Options;
using Spectre.Console;

namespace Nalix.SDK.Client;

internal class Program
{
    // ── Menu groups ───────────────────────────────────────────────────────────
    // Connection
    private const string MENU_CONNECT = "⚡  [[Connection]]  Connect (TCP)";
    private const string MENU_DISCONNECT = "⛔  [[Connection]]  Disconnect (Graceful)";
    private const string MENU_DISCONNECT_HARD = "💥  [[Connection]]  Disconnect (Hard / no frame)";
    // Security
    private const string MENU_HANDSHAKE = "🔐  [[Security]]    Handshake (X25519 ECDH)";
    private const string MENU_RESUME = "♻   [[Security]]    Resume Session";
    private const string MENU_CIPHER = "🔑  [[Security]]    Update Cipher Suite";
    // Ping
    private const string MENU_PING_ONCE = "📡  [[Ping]]        Single Ping";
    private const string MENU_PING_MULTI = "🔁  [[Ping]]        Continuous Ping (batch)";
    // Diagnostics
    private const string MENU_TIMESYNC = "🕐  [[Diag]]        Time Sync";
    // Control frames
    private const string MENU_SEND_CTRL = "📨  [[Control]]     SendControl (pick type)";
    private const string MENU_AWAIT_CTRL = "📬  [[Control]]     SendControl + AwaitControl";
    private const string MENU_REQUEST_RESP = "↔   [[Request]]     RequestAsync[[Control]]";
    // Subscriptions
    private const string MENU_TOGGLE_SUB = "📻  [[Subscribe]]   Toggle On[[Control]] subscription";
    private const string MENU_ONESHOT_SUB = "🎯  [[Subscribe]]   Register OnOnce[[Control]]";
    // UI
    private const string MENU_SHOW_CHART = "📊  [[View]]        Ping History Chart";
    private const string MENU_SERVER_INFO = "ℹ   [[View]]        Session / Transport Info";
    private const string MENU_EXIT = "🚪  Exit";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    private static async Task Main()
    {
        Banner.Render();

        AnsiConsole.MarkupLine("[grey]Welcome to the [aqua bold]Nalix SDK Interactive Client[/].[/]");
        AnsiConsole.MarkupLine("[grey]Exercises every SDK extension: Ping, TimeSync, Handshake, Resume, Cipher, Control, RequestAsync, On[[T]], OnOnce[[T]], GracefulDisconnect.[/]");
        AnsiConsole.WriteLine();

        string host = AnsiConsole.Prompt(
            new TextPrompt<string>("[steelblue1]Server address[/]:")
                .DefaultValue("127.0.0.1")
                .PromptStyle("aqua"));

        ushort port = AnsiConsole.Prompt(
            new TextPrompt<ushort>("[steelblue1]Server port[/]:")
                .DefaultValue((ushort)57206)
                .Validate(p => p > 0
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Port must be > 0[/]")));

        bool encrypt = AnsiConsole.Confirm("[steelblue1]Enable encryption (requires handshake)?[/]", defaultValue: false);

        string? serverPublicKey = null;
        if (encrypt)
        {
            serverPublicKey = AnsiConsole.Prompt(
                new TextPrompt<string>("[steelblue1]Server Public Key[/] [grey](X25519 hex, leave blank to skip)[/]:")
                    .AllowEmpty()
                    .PromptStyle("mediumpurple1"));

            if (string.IsNullOrWhiteSpace(serverPublicKey))
            {
                serverPublicKey = null;
                AnsiConsole.MarkupLine("[yellow]⚠  No public key provided — handshake will be rejected for security.[/]");
            }
        }

        // ── Build objects ─────────────────────────────────────────────────────
        TransportOptions options = new()
        {
            Address = host,
            Port = port,
            EncryptionEnabled = encrypt,
            ServerPublicKey = serverPublicKey,
            ReconnectEnabled = false,
            KeepAliveIntervalMillis = 0   // SDK client does its own keepalive
        };

        StatusBar status = new();
        EventLog log = new();
        PingChart chart = new();
        status.SetServer(host, port);

        await using ClientSession session = new(options);
        using CommandRunner runner = new(session, status, log, chart);

        using CancellationTokenSource appCts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; appCts.Cancel(); };

        log.Info($"Client ready — {host}:{port}  enc={encrypt}");
        log.Info("Use the menu to run SDK extensions. Keepalive is automatic once connected.");

        // ── Main event loop ───────────────────────────────────────────────────
        while (!appCts.Token.IsCancellationRequested)
        {
            RedrawMain(log, chart, status, session.IsConnected);

            string choice;
            try
            {
                choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[aqua bold]≡ SDK Test Menu[/]")
                        .PageSize(20)
                        .HighlightStyle(Style.Parse("aqua bold"))
                        .AddChoices(
                            MENU_CONNECT,
                            MENU_DISCONNECT,
                            MENU_DISCONNECT_HARD,
                            MENU_HANDSHAKE,
                            MENU_RESUME,
                            MENU_CIPHER,
                            MENU_PING_ONCE,
                            MENU_PING_MULTI,
                            MENU_TIMESYNC,
                            MENU_SEND_CTRL,
                            MENU_AWAIT_CTRL,
                            MENU_REQUEST_RESP,
                            MENU_TOGGLE_SUB,
                            MENU_ONESHOT_SUB,
                            MENU_SHOW_CHART,
                            MENU_SERVER_INFO,
                            MENU_EXIT));
            }
            catch (OperationCanceledException) { break; }

            switch (choice)
            {
                case MENU_CONNECT: await runner.ConnectAsync().ConfigureAwait(false); break;
                case MENU_DISCONNECT: await runner.GracefulDisconnectAsync().ConfigureAwait(false); break;
                case MENU_DISCONNECT_HARD: await runner.HardDisconnectAsync().ConfigureAwait(false); break;
                case MENU_HANDSHAKE: await runner.HandshakeAsync().ConfigureAwait(false); break;
                case MENU_RESUME: await runner.ResumeSessionAsync().ConfigureAwait(false); break;
                case MENU_CIPHER: await runner.UpdateCipherAsync().ConfigureAwait(false); break;
                case MENU_PING_ONCE: await runner.PingOnceAsync().ConfigureAwait(false); break;
                case MENU_PING_MULTI: await runner.ContinuousPingAsync().ConfigureAwait(false); break;
                case MENU_TIMESYNC: await runner.TimeSyncAsync().ConfigureAwait(false); break;
                case MENU_SEND_CTRL: await runner.SendControlFrameAsync().ConfigureAwait(false); break;
                case MENU_AWAIT_CTRL: await runner.AwaitControlFrameAsync().ConfigureAwait(false); break;
                case MENU_REQUEST_RESP: await runner.RequestResponseAsync().ConfigureAwait(false); break;
                case MENU_TOGGLE_SUB: runner.ToggleControlSubscription(); break;
                case MENU_ONESHOT_SUB: runner.RegisterOneShotSubscription(); break;
                case MENU_SHOW_CHART: runner.ShowChart(); break;
                case MENU_SERVER_INFO: runner.ShowServerInfo(); break;
                case MENU_EXIT: await appCts.CancelAsync().ConfigureAwait(false); break;
                default:
                    break;
            }
        }

        // ── Graceful shutdown ─────────────────────────────────────────────────
        if (session.IsConnected)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("grey"))
                .StartAsync("Shutting down...", async _ =>
                    await runner.GracefulDisconnectAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[grey]Goodbye! [aqua]Nalix SDK Client[/] exited cleanly.[/]");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void RedrawMain(EventLog log, PingChart chart, StatusBar status, bool connected)
    {
        AnsiConsole.Clear();
        Banner.Render();

        string badge = connected
            ? "[green bold on black] ● ONLINE [/]"
            : "[red bold on black] ○ OFFLINE [/]";

        AnsiConsole.MarkupLine($"  Status: {badge}  [grey dim](Ctrl+C to quit)[/]");
        AnsiConsole.WriteLine();

        chart.Render();
        AnsiConsole.WriteLine();
        log.Render(8);
        AnsiConsole.WriteLine();
        status.Render();
        AnsiConsole.WriteLine();
    }
}
