// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Spectre.Console;

namespace Nalix.SDK.Client.UI;

/// <summary>Renders a scrollable, thread-safe event log panel.</summary>
internal sealed class EventLog
{
    private const int MaxLines = 60;
    private readonly object _lock = new();
    private readonly List<(string Markup, DateTimeOffset Time)> _entries = [];

    public void Info   (string msg) => Add($"[steelblue1]ℹ[/]  [grey]{msg.EscapeMarkup()}[/]");
    public void Success(string msg) => Add($"[green bold]✔[/]  [grey]{msg.EscapeMarkup()}[/]");
    public void Warn   (string msg) => Add($"[gold1]⚠[/]  [yellow]{msg.EscapeMarkup()}[/]");
    public void Error  (string msg) => Add($"[red bold]✘[/]  [red]{msg.EscapeMarkup()}[/]");
    public void Ping   (double ms)
    {
        string color = ms < 50 ? "chartreuse1" : ms < 120 ? "gold1" : "orangered1";
        Add($"[{color}]⟳[/]  [grey]Ping:[/] [{color}]{ms:F2} ms[/]");
    }
    public void Send(string what, string detail) =>
        Add($"[mediumpurple1]↑[/]  [grey]SEND [mediumpurple1]{what.EscapeMarkup()}[/] {detail.EscapeMarkup()}[/]");
    public void Recv(string what, string detail) =>
        Add($"[aqua]↓[/]  [grey]RECV [aqua]{what.EscapeMarkup()}[/] {detail.EscapeMarkup()}[/]");

    private void Add(string markup)
    {
        lock (_lock)
        {
            _entries.Add((markup, DateTimeOffset.Now));
            if (_entries.Count > MaxLines)
            {
                _entries.RemoveAt(0);
            }
        }
    }

    public void Render(int visibleLines = 18)
    {
        List<(string Markup, DateTimeOffset Time)> snapshot;
        lock (_lock) { snapshot = new List<(string, DateTimeOffset)>(_entries); }

        int start = Math.Max(0, snapshot.Count - visibleLines);
        var rows  = snapshot.Skip(start).ToList();

        var panel = new Panel(BuildContent(rows))
        {
            Header      = new PanelHeader(" [aqua bold]● EVENT LOG[/] "),
            Border      = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            Padding     = new Padding(1, 0),
            Expand      = true
        };

        AnsiConsole.Write(panel);
    }

    private static string BuildContent(IList<(string Markup, DateTimeOffset Time)> rows)
    {
        if (rows.Count == 0)
        {
            return "[grey italic]No events yet...[/]";
        }

        var sb = new System.Text.StringBuilder();
        foreach (var (markup, time) in rows)
        {
            sb.AppendLine($"[grey]{time:HH:mm:ss.fff}[/]  {markup}");
        }
        return sb.ToString().TrimEnd();
    }
}
