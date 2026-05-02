// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
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
    private readonly ConnectionHub _hub;

    // ── Cached state ─────────────────────────────────────────────────────────
    private static readonly Style s_grey = Style.Parse("grey");
    private string[]? _cachedLines;
    private int _cachedPage = -1;
    private long _cachedTick;

    public DashboardRenderer(ConnectionHub hub, IPageFormatter[] pages)
    {
        _hub = hub;
        _pages = pages;
    }

    /// <summary>Runs the live dashboard until Q is pressed or the token is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[grey dim]  ↑ ↓ / PgUp PgDn scroll   ← → page   Q back[/]");
        await Task.Delay(150, ct).ConfigureAwait(false);

        int page = 0;
        int scroll = 0;

        const int Chrome = 6;
        static int viewLines() => Math.Max(5, Console.WindowHeight - Chrome);

        using CancellationTokenSource exitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = Task.Run(async () =>
        {
            while (!exitCts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo k = Console.ReadKey(intercept: true);
                    switch (k.Key)
                    {
                        case ConsoleKey.Q:
                            await exitCts.CancelAsync().ConfigureAwait(false);
                            return;
                        case ConsoleKey.RightArrow:
                            page = (page + 1) % _pages.Length; scroll = 0; break;
                        case ConsoleKey.LeftArrow:
                            page = (page - 1 + _pages.Length) % _pages.Length; scroll = 0; break;
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.PageUp:
                            scroll = Math.Max(0, scroll - (k.Key == ConsoleKey.PageUp ? viewLines() : 5)); break;
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.PageDown:
                            scroll += k.Key == ConsoleKey.PageDown ? viewLines() : 5; break;
                        case ConsoleKey.Home:
                            scroll = 0; break;
                        case ConsoleKey.None:
                            break;
                        case ConsoleKey.Backspace:
                            break;
                        case ConsoleKey.Tab:
                            break;
                        case ConsoleKey.Clear:
                            break;
                        case ConsoleKey.Enter:
                            break;
                        case ConsoleKey.Pause:
                            break;
                        case ConsoleKey.Escape:
                            break;
                        case ConsoleKey.Spacebar:
                            break;
                        case ConsoleKey.End:
                            break;
                        case ConsoleKey.Select:
                            break;
                        case ConsoleKey.Print:
                            break;
                        case ConsoleKey.Execute:
                            break;
                        case ConsoleKey.PrintScreen:
                            break;
                        case ConsoleKey.Insert:
                            break;
                        case ConsoleKey.Delete:
                            break;
                        case ConsoleKey.Help:
                            break;
                        case ConsoleKey.D0:
                            break;
                        case ConsoleKey.D1:
                            break;
                        case ConsoleKey.D2:
                            break;
                        case ConsoleKey.D3:
                            break;
                        case ConsoleKey.D4:
                            break;
                        case ConsoleKey.D5:
                            break;
                        case ConsoleKey.D6:
                            break;
                        case ConsoleKey.D7:
                            break;
                        case ConsoleKey.D8:
                            break;
                        case ConsoleKey.D9:
                            break;
                        case ConsoleKey.A:
                            break;
                        case ConsoleKey.B:
                            break;
                        case ConsoleKey.C:
                            break;
                        case ConsoleKey.D:
                            break;
                        case ConsoleKey.E:
                            break;
                        case ConsoleKey.F:
                            break;
                        case ConsoleKey.G:
                            break;
                        case ConsoleKey.H:
                            break;
                        case ConsoleKey.I:
                            break;
                        case ConsoleKey.J:
                            break;
                        case ConsoleKey.K:
                            break;
                        case ConsoleKey.L:
                            break;
                        case ConsoleKey.M:
                            break;
                        case ConsoleKey.N:
                            break;
                        case ConsoleKey.O:
                            break;
                        case ConsoleKey.P:
                            break;
                        case ConsoleKey.R:
                            break;
                        case ConsoleKey.S:
                            break;
                        case ConsoleKey.T:
                            break;
                        case ConsoleKey.U:
                            break;
                        case ConsoleKey.V:
                            break;
                        case ConsoleKey.W:
                            break;
                        case ConsoleKey.X:
                            break;
                        case ConsoleKey.Y:
                            break;
                        case ConsoleKey.Z:
                            break;
                        case ConsoleKey.LeftWindows:
                            break;
                        case ConsoleKey.RightWindows:
                            break;
                        case ConsoleKey.Applications:
                            break;
                        case ConsoleKey.Sleep:
                            break;
                        case ConsoleKey.NumPad0:
                            break;
                        case ConsoleKey.NumPad1:
                            break;
                        case ConsoleKey.NumPad2:
                            break;
                        case ConsoleKey.NumPad3:
                            break;
                        case ConsoleKey.NumPad4:
                            break;
                        case ConsoleKey.NumPad5:
                            break;
                        case ConsoleKey.NumPad6:
                            break;
                        case ConsoleKey.NumPad7:
                            break;
                        case ConsoleKey.NumPad8:
                            break;
                        case ConsoleKey.NumPad9:
                            break;
                        case ConsoleKey.Multiply:
                            break;
                        case ConsoleKey.Add:
                            break;
                        case ConsoleKey.Separator:
                            break;
                        case ConsoleKey.Subtract:
                            break;
                        case ConsoleKey.Decimal:
                            break;
                        case ConsoleKey.Divide:
                            break;
                        case ConsoleKey.F1:
                            break;
                        case ConsoleKey.F2:
                            break;
                        case ConsoleKey.F3:
                            break;
                        case ConsoleKey.F4:
                            break;
                        case ConsoleKey.F5:
                            break;
                        case ConsoleKey.F6:
                            break;
                        case ConsoleKey.F7:
                            break;
                        case ConsoleKey.F8:
                            break;
                        case ConsoleKey.F9:
                            break;
                        case ConsoleKey.F10:
                            break;
                        case ConsoleKey.F11:
                            break;
                        case ConsoleKey.F12:
                            break;
                        case ConsoleKey.F13:
                            break;
                        case ConsoleKey.F14:
                            break;
                        case ConsoleKey.F15:
                            break;
                        case ConsoleKey.F16:
                            break;
                        case ConsoleKey.F17:
                            break;
                        case ConsoleKey.F18:
                            break;
                        case ConsoleKey.F19:
                            break;
                        case ConsoleKey.F20:
                            break;
                        case ConsoleKey.F21:
                            break;
                        case ConsoleKey.F22:
                            break;
                        case ConsoleKey.F23:
                            break;
                        case ConsoleKey.F24:
                            break;
                        case ConsoleKey.BrowserBack:
                            break;
                        case ConsoleKey.BrowserForward:
                            break;
                        case ConsoleKey.BrowserRefresh:
                            break;
                        case ConsoleKey.BrowserStop:
                            break;
                        case ConsoleKey.BrowserSearch:
                            break;
                        case ConsoleKey.BrowserFavorites:
                            break;
                        case ConsoleKey.BrowserHome:
                            break;
                        case ConsoleKey.VolumeMute:
                            break;
                        case ConsoleKey.VolumeDown:
                            break;
                        case ConsoleKey.VolumeUp:
                            break;
                        case ConsoleKey.MediaNext:
                            break;
                        case ConsoleKey.MediaPrevious:
                            break;
                        case ConsoleKey.MediaStop:
                            break;
                        case ConsoleKey.MediaPlay:
                            break;
                        case ConsoleKey.LaunchMail:
                            break;
                        case ConsoleKey.LaunchMediaSelect:
                            break;
                        case ConsoleKey.LaunchApp1:
                            break;
                        case ConsoleKey.LaunchApp2:
                            break;
                        case ConsoleKey.Oem1:
                            break;
                        case ConsoleKey.OemPlus:
                            break;
                        case ConsoleKey.OemComma:
                            break;
                        case ConsoleKey.OemMinus:
                            break;
                        case ConsoleKey.OemPeriod:
                            break;
                        case ConsoleKey.Oem2:
                            break;
                        case ConsoleKey.Oem3:
                            break;
                        case ConsoleKey.Oem4:
                            break;
                        case ConsoleKey.Oem5:
                            break;
                        case ConsoleKey.Oem6:
                            break;
                        case ConsoleKey.Oem7:
                            break;
                        case ConsoleKey.Oem8:
                            break;
                        case ConsoleKey.Oem102:
                            break;
                        case ConsoleKey.Process:
                            break;
                        case ConsoleKey.Packet:
                            break;
                        case ConsoleKey.Attention:
                            break;
                        case ConsoleKey.CrSel:
                            break;
                        case ConsoleKey.ExSel:
                            break;
                        case ConsoleKey.EraseEndOfFile:
                            break;
                        case ConsoleKey.Play:
                            break;
                        case ConsoleKey.Zoom:
                            break;
                        case ConsoleKey.NoName:
                            break;
                        case ConsoleKey.Pa1:
                            break;
                        case ConsoleKey.OemClear:
                            break;
                        default:
                            break;
                    }
                }
                await Task.Delay(40).ConfigureAwait(false);
            }
        }, CancellationToken.None);

        await AnsiConsole.Live(this.Build(page, scroll, viewLines()))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                while (!exitCts.Token.IsCancellationRequested)
                {
                    ctx.UpdateTarget(this.Build(page, scroll, viewLines()));
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
    private IRenderable Build(int page, int scroll, int viewLines)
    {
        // Tab bar
        StringBuilder tabSb = new(256);
        for (int i = 0; i < _pages.Length; i++)
        {
            if (i > 0)
            {
                _ = tabSb.Append("[grey]|[/]");
            }

            _ = tabSb.Append(i == page
                ? $"[steelblue1 bold] {_pages[i].Label} [/]"
                : $"[grey dim] {_pages[i].Label} [/]");
        }
        _ = tabSb.Append($"   [grey dim]{DateTime.Now:HH:mm:ss}[/]");

        // Lines (cached)
        string[] lines = this.GetLines(page);
        int totalLines = lines.Length;
        int maxScroll = Math.Max(0, totalLines - viewLines);
        if (scroll > maxScroll)
        {
            scroll = maxScroll;
        }

        int start = scroll;
        int end = Math.Min(scroll + viewLines, totalLines);

        // Visible slice
        StringBuilder sliceSb = new(2048);
        for (int i = start; i < end; i++)
        {
            if (i > start)
            {
                _ = sliceSb.Append('\n');
            }

            _ = sliceSb.Append(lines[i]);
        }

        // Scroll hint
        bool canUp = scroll > 0;
        bool canDown = scroll < maxScroll;
        string scrollHint = totalLines <= viewLines
            ? "[grey dim](all content visible)[/]"
            : $"[grey dim]line {scroll + 1}–{end}/{totalLines}  " +
              $"{(canUp ? "[white]↑[/]" : "·")} " +
              $"{(canDown ? "[white]↓[/]" : "·")} scroll[/]";

        return new Rows(
            new Markup(tabSb.ToString()),
            new Text(""),
            new Panel(new Markup($"[grey]{sliceSb.ToString().EscapeMarkup()}[/]"))
            {
                Header = new PanelHeader($" {_pages[page].Label}  {DateTime.Now:HH:mm:ss} "),
                Border = BoxBorder.Rounded,
                BorderStyle = s_grey,
                Padding = new Padding(1, 0),
                Expand = true
            },
            new Markup(scrollHint));
    }

    // ── Line cache (900ms TTL) ───────────────────────────────────────────────

    private string[] GetLines(int page)
    {
        long tick = System.Environment.TickCount64;

        if (_cachedLines is not null && _cachedPage == page && (tick - _cachedTick) < 900)
        {
            return _cachedLines;
        }

        string text = _pages[page].Format(_hub);
        _cachedLines = text.Split('\n');
        _cachedPage = page;
        _cachedTick = tick;
        return _cachedLines;
    }
}
