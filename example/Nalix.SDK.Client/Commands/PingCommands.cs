// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Client.Core;
using Nalix.SDK.Client.UI;
using Spectre.Console;

namespace Nalix.SDK.Client.Commands;

/// <summary>Single ping and continuous ping commands.</summary>
internal sealed class PingCommands
{
    private readonly ClientSession _client;
    private readonly StatusBar     _status;
    private readonly EventLog      _log;
    private readonly PingChart     _chart;

    public PingCommands(ClientSession client, StatusBar status, EventLog log, PingChart chart)
    {
        _client = client;
        _status = status;
        _log    = log;
        _chart  = chart;
    }

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

    private bool RequireConnected()
    {
        if (_client.IsConnected) return true;
        _log.Error("Not connected — use [Connect] first.");
        return false;
    }
}
