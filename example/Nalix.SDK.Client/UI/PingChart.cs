// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Spectre.Console;

namespace Nalix.SDK.Client.UI;

/// <summary>
/// Renders a compact sparkline-style ping history bar inside a Spectre panel.
/// Keeps the last N RTT samples and renders a bar chart.
/// </summary>
internal sealed class PingChart
{
    private const int MaxSamples = 30;
    private readonly object  _lock    = new();
    private readonly Queue<double> _samples = new();
    private double _min = double.MaxValue;
    private double _max = 0;
    private double _avg = 0;
    private long   _count = 0;

    public void AddSample(double ms)
    {
        lock (_lock)
        {
            _samples.Enqueue(ms);
            if (_samples.Count > MaxSamples) { _samples.Dequeue(); }
            if (ms < _min) _min = ms;
            if (ms > _max) _max = ms;
            _count++;
            _avg = (_avg * (_count - 1) + ms) / _count;
        }
    }

    public void Render()
    {
        double[] snap;
        double min, max, avg;

        lock (_lock)
        {
            snap = [.. _samples];
            min  = _min == double.MaxValue ? 0 : _min;
            max  = _max;
            avg  = _avg;
        }

        if (snap.Length == 0)
        {
            var empty = new Panel("[grey italic]No ping samples yet. Run [mediumpurple1]Continuous Ping[/] to populate.[/]")
            {
                Header      = new PanelHeader(" [chartreuse1 bold]◈ PING HISTORY[/] "),
                Border      = BoxBorder.Rounded,
                BorderStyle = Style.Parse("grey"),
                Padding     = new Padding(1, 0),
                Expand      = true
            };
            AnsiConsole.Write(empty);
            return;
        }

        // Build a bar chart
        var chart = new BarChart()
            .Width(78)
            .Label($"[grey]RTT (ms)  Min=[chartreuse1]{min:F1}[/]  Avg=[gold1]{avg:F1}[/]  Max=[orangered1]{max:F1}[/][/]")
            .CenterLabel();

        for (int i = 0; i < snap.Length; i++)
        {
            double val   = snap[i];
            Color  color = val < 50 ? Color.Chartreuse1 : val < 120 ? Color.Gold1 : Color.OrangeRed1;
            chart.AddItem($"#{i + 1}", val, color);
        }

        var panel = new Panel(chart)
        {
            Header      = new PanelHeader(" [chartreuse1 bold]◈ PING HISTORY (last 30)[/] "),
            Border      = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            Padding     = new Padding(1, 0),
            Expand      = true
        };

        AnsiConsole.Write(panel);
    }
}
