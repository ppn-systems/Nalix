// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.Services;
using Nalix.SDK.Client.UI;
using Nalix.SDK.Options;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>Chart rendering and server info display commands.</summary>
internal sealed class ViewCommands
{
    private readonly ClientSession _client;
    private readonly EventLog _log;
    private readonly PingChart _chart;
    private readonly StatusBar _status;
    private readonly SubscriptionManager _subs;

    public ViewCommands(ClientSession client, EventLog log, PingChart chart,
                        StatusBar status, SubscriptionManager subs)
    {
        _client = client;
        _log = log;
        _chart = chart;
        _status = status;
        _subs = subs;
    }

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
        TransportOptions opts = _client.Session.Options;

        Table tbl = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Title("[aqua bold]Session & Transport Options[/]")
            .AddColumn(new TableColumn("[grey]Property[/]").Centered())
            .AddColumn(new TableColumn("[grey]Value[/]").Centered());

        _ = tbl.AddRow("[steelblue1]Address[/]", $"[white]{opts.Address}[/]");
        _ = tbl.AddRow("[steelblue1]Port[/]", $"[white]{opts.Port}[/]");
        _ = tbl.AddRow("[steelblue1]Connected[/]", _client.IsConnected ? "[green]Yes[/]" : "[red]No[/]");
        _ = tbl.AddRow("[steelblue1]Encryption[/]", opts.EncryptionEnabled ? "[green]Enabled[/]" : "[yellow]Disabled[/]");
        _ = tbl.AddRow("[steelblue1]Algorithm[/]", $"[mediumpurple1]{opts.Algorithm}[/]");
        _ = tbl.AddRow("[steelblue1]Server PublicKey[/]", string.IsNullOrEmpty(opts.ServerPublicKey)
            ? "[grey]not set[/]"
            : $"[mediumpurple1]{opts.ServerPublicKey[..Math.Min(16, opts.ServerPublicKey.Length)]}…[/]");
        _ = tbl.AddRow("[steelblue1]Compression[/]", opts.CompressionEnabled ? "[green]Enabled[/]" : "[grey]Off[/]");
        _ = tbl.AddRow("[steelblue1]Compression Thresh[/]", $"[white]{opts.CompressionThreshold}B[/]");
        _ = tbl.AddRow("[steelblue1]Reconnect[/]", opts.ReconnectEnabled ? "[green]Yes[/]" : "[grey]No[/]");
        _ = tbl.AddRow("[steelblue1]Reconnect Attempts[/]", $"[white]{opts.ReconnectMaxAttempts}[/]");
        _ = tbl.AddRow("[steelblue1]Connect Timeout[/]", $"[white]{opts.ConnectTimeoutMillis}ms[/]");
        _ = tbl.AddRow("[steelblue1]Resume Enabled[/]", opts.ResumeEnabled ? "[green]Yes[/]" : "[grey]No[/]");
        _ = tbl.AddRow("[steelblue1]Resume Timeout[/]", $"[white]{opts.ResumeTimeoutMillis}ms[/]");
        _ = tbl.AddRow("[steelblue1]Session Token[/]", $"[white]{opts.SessionToken}[/]");
        _ = tbl.AddRow("[steelblue1]Control Sub[/]", _subs.IsControlSubActive ? "[green]Active[/]" : "[grey]Inactive[/]");

        AnsiConsole.Write(tbl);
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        _ = Console.ReadKey(true);
    }
}
