// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Spectre.Console;

namespace Nalix.SDK.Client.UI;

/// <summary>Thread-safe status bar that shows connection info + live ping in the footer.</summary>
internal sealed class StatusBar
{
    private readonly Lock _lock = new();

    private string _host = "127.0.0.1";
    private ushort _port = 57206;
    private bool _connected;
    private double _lastPingMs;
    private long _pingsSent;
    private long _errors;

    public void SetServer(string host, ushort port)
    {
        lock (_lock) { _host = host; _port = port; }
    }

    public void SetConnected(bool connected)
    {
        lock (_lock) { _connected = connected; }
    }

    public void UpdatePing(double ms)
    {
        lock (_lock) { _lastPingMs = ms; _pingsSent++; }
    }

    public void IncrementErrors()
    {
        lock (_lock) { _errors++; }
    }

    public void Render()
    {
        string host;
        ushort port;
        bool conn;
        double ping;
        long sent;
        long errs;

        lock (_lock)
        {
            host = _host; port = _port; conn = _connected;
            ping = _lastPingMs; sent = _pingsSent; errs = _errors;
        }

        string connColor = conn ? "green bold" : "red bold";
        string connText = conn ? "●  ONLINE" : "○  OFFLINE";
        string pingDisplay = sent == 0 ? "[grey]--[/]" : $"[{(ping < 50 ? "chartreuse1" : ping < 120 ? "gold1" : "orangered1")}]{ping:F1} ms[/]";

        Table table = new() { Border = TableBorder.None, ShowHeaders = false };
        _ = table.AddColumn(new TableColumn("").Centered());
        _ = table.AddRow(new Markup(
            $"[grey]Server:[/] [steelblue1]{host}:{port}[/]  " +
            $"[grey]Status:[/] [{connColor}]{connText}[/]  " +
            $"[grey]Ping:[/] {pingDisplay}  " +
            $"[grey]Sent:[/] [mediumpurple1]{sent}[/]  " +
            $"[grey]Errors:[/] [red]{errs}[/]"));

        AnsiConsole.Write(new Rule() { Style = Style.Parse("grey dim") });
        AnsiConsole.Write(table);
    }
}
