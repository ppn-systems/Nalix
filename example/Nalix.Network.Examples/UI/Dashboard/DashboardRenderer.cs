// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Dashboard.Pages;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Nalix.Network.Examples.UI.Dashboard;

/// <summary>
/// Manages the Spectre.Console Live dashboard with paged navigation and scrolling.
/// Depends on <see cref="IPageFormatter"/> for content — no formatting logic here.
/// </summary>
internal sealed class DashboardRenderer
{
    private readonly IPageFormatter[] _pages;
    private readonly ConnectionHub    _hub;

    // ── Cached state ─────────────────────────────────────────────────────────
    private static readonly Style s_grey = Style.Parse("grey");
    private string[]? _cachedLines;
    private int       _cachedPage = -1;
    private long      _cachedTick;

    public DashboardRenderer(ConnectionHub hub, IPageFormatter[] pages)
    {
        _hub   = hub;
        _pages = pages;
    }

    /// <summary>Runs the live dashboard until Q is pressed or the token is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[grey dim]  ↑ ↓ / PgUp PgDn scroll   ← → page   Q back[/]");
        await Task.Delay(150).ConfigureAwait(false);

        int page   = 0;
        int scroll = 0;

        const int Chrome = 6;
        int viewLines() => Math.Max(5, Console.WindowHeight - Chrome);

        using var exitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = Task.Run(async () =>
        {
            while (!exitCts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(intercept: true);
                    switch (k.Key)
                    {
                        case ConsoleKey.Q:
                            exitCts.Cancel(); return;
                        case ConsoleKey.RightArrow:
                            page = (page + 1) % _pages.Length; scroll = 0; break;
                        case ConsoleKey.LeftArrow:
                            page = (page - 1 + _pages.Length) % _pages.Length; scroll = 0; break;
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.PageUp:
                            scroll = Math.Max(0, scroll - (k.Key == ConsoleKey.PageUp ? viewLines() : 5)); break;
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.PageDown:
                            scroll += (k.Key == ConsoleKey.PageDown ? viewLines() : 5); break;
                        case ConsoleKey.Home:
                            scroll = 0; break;
                    }
                }
                await Task.Delay(40).ConfigureAwait(false);
            }
        }, CancellationToken.None);

        await AnsiConsole.Live(Build(page, scroll, viewLines()))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                while (!exitCts.Token.IsCancellationRequested)
                {
                    ctx.UpdateTarget(Build(page, scroll, viewLines()));
                    ctx.Refresh();
                    try { await Task.Delay(1000, exitCts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }).ConfigureAwait(false);

        _cachedLines = null;
        _cachedPage = -1;

        AnsiConsole.WriteLine();
    }

    // ── Build renderable ─────────────────────────────────────────────────────

    private IRenderable Build(int page, int scroll, int viewLines)
    {
        // Tab bar
        var tabSb = new System.Text.StringBuilder(256);
        for (int i = 0; i < _pages.Length; i++)
        {
            if (i > 0) tabSb.Append("[grey]|[/]");
            tabSb.Append(i == page
                ? $"[steelblue1 bold] {_pages[i].Label} [/]"
                : $"[grey dim] {_pages[i].Label} [/]");
        }
        tabSb.Append($"   [grey dim]{DateTime.Now:HH:mm:ss}[/]");

        // Lines (cached)
        string[] lines = GetLines(page);
        int totalLines  = lines.Length;
        int maxScroll   = Math.Max(0, totalLines - viewLines);
        if (scroll > maxScroll) scroll = maxScroll;
        int start = scroll;
        int end   = Math.Min(scroll + viewLines, totalLines);

        // Visible slice
        var sliceSb = new System.Text.StringBuilder(2048);
        for (int i = start; i < end; i++)
        {
            if (i > start) sliceSb.Append('\n');
            sliceSb.Append(lines[i]);
        }

        // Scroll hint
        bool canUp   = scroll > 0;
        bool canDown = scroll < maxScroll;
        string scrollHint = totalLines <= viewLines
            ? "[grey dim](all content visible)[/]"
            : $"[grey dim]line {scroll + 1}–{end}/{totalLines}  " +
              $"{(canUp   ? "[white]↑[/]" : "·")} " +
              $"{(canDown ? "[white]↓[/]" : "·")} scroll[/]";

        return new Rows(
            new Markup(tabSb.ToString()),
            new Text(""),
            new Panel(new Markup($"[grey]{sliceSb.ToString().EscapeMarkup()}[/]"))
            {
                Header      = new PanelHeader($" {_pages[page].Label}  {DateTime.Now:HH:mm:ss} "),
                Border      = BoxBorder.Rounded,
                BorderStyle = s_grey,
                Padding     = new Padding(1, 0),
                Expand      = true
            },
            new Markup(scrollHint));
    }

    // ── Line cache (900ms TTL) ───────────────────────────────────────────────

    private string[] GetLines(int page)
    {
        long tick = System.Environment.TickCount64;

        if (_cachedLines is not null && _cachedPage == page && (tick - _cachedTick) < 900)
            return _cachedLines;

        string text = _pages[page].Format(_hub);
        _cachedLines = text.Split('\n');
        _cachedPage  = page;
        _cachedTick  = tick;
        return _cachedLines;
    }
}
