// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.Services;
using Nalix.SDK.Client.UI;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>Chart rendering and server info display commands.</summary>
internal sealed class ViewCommands
{
    private readonly ClientSession      _client;
    private readonly EventLog           _log;
    private readonly PingChart          _chart;
    private readonly StatusBar          _status;
    private readonly SubscriptionManager _subs;

    public ViewCommands(ClientSession client, EventLog log, PingChart chart,
                        StatusBar status, SubscriptionManager subs)
    {
        _client = client;
        _log    = log;
        _chart  = chart;
        _status = status;
        _subs   = subs;
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
        tbl.AddRow("[steelblue1]Control Sub[/]",      _subs.IsControlSubActive ? "[green]Active[/]" : "[grey]Inactive[/]");

        AnsiConsole.Write(tbl);
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        _ = Console.ReadKey(true);
    }
}
