// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Nalix.Network.Examples.UI;

internal static class ServerConsole
{
    // ── Pages ─────────────────────────────────────────────────────────────────
    private enum Page { Process, ConnectionHub, ObjectPool, BufferPool, TaskManager, InstanceManager }
    private static readonly string[] PageLabels = ["Process", "Conn Hub", "Obj Pool", "Buf Pool", "Tasks", "Instances"];

    // ── Cached styles (avoid Style.Parse per tick) ────────────────────────────
    private static readonly Style s_grey       = Style.Parse("grey");
    private static readonly Style s_greyDim    = Style.Parse("grey dim");
    private static readonly Style s_highlight  = Style.Parse("steelblue1 bold");

    // ── Reusable StringBuilder to eliminate per-tick allocations ──────────────
    [ThreadStatic] private static StringBuilder? t_sb;

    // ── Cached rendered lines per page (avoid full rebuild every tick) ────────
    private static string[]? s_cachedLines;
    private static int       s_cachedPage = -1;
    private static long      s_cachedTick;

    // ── Menu ──────────────────────────────────────────────────────────────────
    private const string M_DASHBOARD = "Live dashboard (paged, real-time)";
    private const string M_SNAPSHOT  = "Snapshot (one-shot print)";
    private const string M_EXIT      = "Stop server";

    // ── Main menu ─────────────────────────────────────────────────────────────

    public static async Task RunMenuAsync(ConnectionHub hub, CancellationTokenSource shutdown)
    {
        await Task.Delay(300).ConfigureAwait(false);

        while (!shutdown.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();
            string choice;
            try
            {
                choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[grey]Server  [dim]{DateTime.Now:HH:mm:ss}[/][/]")
                        .PageSize(6)
                        .HighlightStyle(s_highlight)
                        .AddChoices(M_DASHBOARD, M_SNAPSHOT, M_EXIT));
            }
            catch { break; }

            switch (choice)
            {
                case M_DASHBOARD: await RunPagedDashboardAsync(hub, shutdown.Token).ConfigureAwait(false); break;
                case M_SNAPSHOT:  PrintAllPages(hub); break;
                case M_EXIT:
                    AnsiConsole.MarkupLine("[grey]Stopping...[/]");
                    shutdown.Cancel();
                    return;
            }
        }
    }

    // ── Paged live dashboard ──────────────────────────────────────────────────

    private static async Task RunPagedDashboardAsync(ConnectionHub hub, CancellationToken ct)
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
                            page = (page + 1) % PageLabels.Length; scroll = 0; break;
                        case ConsoleKey.LeftArrow:
                            page = (page - 1 + PageLabels.Length) % PageLabels.Length; scroll = 0; break;
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

        await AnsiConsole.Live(BuildPageRenderable(hub, (Page)page, scroll, viewLines()))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                while (!exitCts.Token.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildPageRenderable(hub, (Page)page, scroll, viewLines()));
                    ctx.Refresh();
                    try { await Task.Delay(1000, exitCts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }).ConfigureAwait(false);

        // Clear cache on exit
        s_cachedLines = null;
        s_cachedPage = -1;

        AnsiConsole.WriteLine();
    }

    // ── Build one renderable (hot path — avoid allocations) ──────────────────

    private static IRenderable BuildPageRenderable(ConnectionHub hub, Page page, int scroll, int viewLines)
    {
        // Tab bar — built with stackalloc-style approach
        int pageIdx = (int)page;
        StringBuilder tabSb = RentSb();
        for (int i = 0; i < PageLabels.Length; i++)
        {
            if (i > 0) tabSb.Append("[grey]|[/]");
            tabSb.Append(i == pageIdx ? $"[steelblue1 bold] {PageLabels[i]} [/]" : $"[grey dim] {PageLabels[i]} [/]");
        }
        tabSb.Append(CultureInfo.InvariantCulture, $"   [grey dim]{DateTime.Now:HH:mm:ss}[/]");
        string tabBar = tabSb.ToString();

        // Get or rebuild lines for active page
        string[] lines = GetCachedLines(hub, page);
        int totalLines = lines.Length;

        // Clamp scroll
        int maxScroll = Math.Max(0, totalLines - viewLines);
        if (scroll > maxScroll) scroll = maxScroll;
        int start = scroll;
        int end   = Math.Min(scroll + viewLines, totalLines);

        // Slice visible lines only (avoid copying entire array)
        StringBuilder sliceSb = RentSb();
        for (int i = start; i < end; i++)
        {
            if (i > start) sliceSb.Append('\n');
            sliceSb.Append(lines[i]);
        }

        // Scroll indicator
        bool canUp   = scroll > 0;
        bool canDown = scroll < maxScroll;
        string scrollHint = totalLines <= viewLines
            ? "[grey dim](all content visible)[/]"
            : $"[grey dim]line {scroll + 1}–{end}/{totalLines}  " +
              $"{(canUp   ? "[white]↑[/]" : "·")} " +
              $"{(canDown ? "[white]↓[/]" : "·")} scroll[/]";

        return new Rows(
            new Markup(tabBar),
            new Text(""),
            new Panel(new Markup($"[grey]{sliceSb.ToString().EscapeMarkup()}[/]"))
            {
                Header      = new PanelHeader($" {PageLabels[pageIdx]}  {DateTime.Now:HH:mm:ss} "),
                Border      = BoxBorder.Rounded,
                BorderStyle = s_grey,
                Padding     = new Padding(1, 0),
                Expand      = true
            },
            new Markup(scrollHint));
    }

    // ── Line cache — rebuild only on page change or every tick ────────────────

    private static string[] GetCachedLines(ConnectionHub hub, Page page)
    {
        long tick = System.Environment.TickCount64;
        int p = (int)page;

        // Reuse cache if same page and within same 900ms window
        if (s_cachedLines is not null && s_cachedPage == p && (tick - s_cachedTick) < 900)
        {
            return s_cachedLines;
        }

        string text = FormatPageData(hub, page);
        // Split without LINQ — use simple split
        string[] lines = text.Split('\n');
        s_cachedLines = lines;
        s_cachedPage = p;
        s_cachedTick = tick;
        return lines;
    }

    // ── Format report data per page (only active page is evaluated) ──────────

    private static string FormatPageData(ConnectionHub hub, Page page) => page switch
    {
        Page.Process         => FormatProcessData(),
        Page.ConnectionHub   => FormatDict(hub.GetReportData(), "ConnectionHub"),
        Page.ObjectPool      => FormatObjPool(),
        Page.BufferPool      => FormatBufPool(),
        Page.TaskManager     => FormatTaskMgr(),
        Page.InstanceManager => FormatDict(InstanceManager.Instance.GetReportData(), "InstanceManager"),
        _                    => ""
    };

    private static string FormatObjPool()
    {
        var mgr = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>();
        return mgr is null ? "(ObjectPoolManager not registered)" : FormatDict(mgr.GetReportData(), "ObjectPoolManager");
    }

    private static string FormatBufPool()
    {
        var mgr = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();
        return mgr is null ? "(BufferPoolManager not registered)" : FormatDict(mgr.GetReportData(), "BufferPoolManager");
    }

    private static string FormatTaskMgr()
    {
        var mgr = InstanceManager.Instance.GetExistingInstance<TaskManager>();
        return mgr is null ? "(TaskManager not registered)" : FormatDict(mgr.GetReportData(), "TaskManager");
    }

    // ── Optimized dictionary → text (reuses StringBuilder, no LINQ) ──────────

    private static string FormatDict(IDictionary<string, object> data, string header)
    {
        StringBuilder sb = RentSb();
        sb.Append('['); sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sb.Append("] "); sb.Append(header); sb.AppendLine(":");
        sb.AppendLine("---------------------------------------------------------------------");

        foreach (var kvp in data)
        {
            AppendValue(sb, kvp.Key, kvp.Value, 0);
        }

        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string key, object? value, int indent)
    {
        // Inline pad — avoid "new string(' ', n)" allocation
        AppendPad(sb, indent);

        switch (value)
        {
            case IDictionary<string, object> dict:
                AppendKeyColon(sb, key, indent);
                sb.AppendLine();
                foreach (var kvp in dict) AppendValue(sb, kvp.Key, kvp.Value, indent + 1);
                break;

            case IList<Dictionary<string, object>> list:
                sb.AppendLine();
                AppendPad(sb, indent);
                sb.Append(key); sb.Append(" ("); sb.Append(list.Count); sb.AppendLine("):");
                if (list.Count > 0) AppendCompactTable(sb, key, list);
                break;

            case IDictionary<string, int> intDict:
                AppendKeyColon(sb, key, indent);
                sb.AppendLine();
                foreach (var kvp in intDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            case IDictionary<string, long> longDict:
                AppendKeyColon(sb, key, indent);
                sb.AppendLine();
                foreach (var kvp in longDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            default:
                AppendKeyColon(sb, key, indent);
                sb.Append(' ');
                AppendScalar(sb, value);
                sb.AppendLine();
                break;
        }
    }

    // ── Compact table formatters (match lib style) ───────────────────────────

    private static void AppendCompactTable(StringBuilder sb, string key, IList<Dictionary<string, object>> list)
    {
        // Dispatch to known table layouts, else fallback
        if (key is "Pools" && list[0].ContainsKey("Gets"))
        {
            AppendObjectPoolTable(sb, list);
        }
        else if (key is "Pools" && list[0].ContainsKey("BufferSize"))
        {
            AppendBufferPoolTable(sb, list);
        }
        else if (key is "Recurring")
        {
            AppendRecurringTable(sb, list);
        }
        else if (key is "TopRunningWorkers")
        {
            AppendWorkersTable(sb, list);
        }
        else if (key is "SampleConnections")
        {
            AppendConnectionsTable(sb, list);
        }
        else if (key is "Instances")
        {
            AppendInstancesTable(sb, list);
        }
        else if (key is "TopRecurringByFailures")
        {
            AppendTopRecurringTable(sb, list);
        }
        else if (key is "UnhealthyPools")
        {
            AppendUnhealthyTable(sb, list);
        }
        else
        {
            AppendFallbackTable(sb, list);
        }
    }

    // ObjectPool: TYPE | STORAGE (A/M) | TRAFFIC (G/R) | HIT%  | STATUS
    private static void AppendObjectPoolTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                         | STORAGE (A/M)     | TRAFFIC (G/R)     | HIT%   | STATUS");
        sb.AppendLine("-----------------------------+-------------------+-------------------+--------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type = Truncate(Str(d, "Type"), 28).PadRight(28);
            long avail  = Long(d, "Available");
            long max    = Long(d, "MaxCapacity");
            long gets   = Long(d, "Gets");
            long hits   = Long(d, "Hits");
            double hit  = Dbl(d, "HitRate");
            string st   = Str(d, "Status");
            string storage = $"{Compact(avail)} / {Compact(max)}";
            string traffic = $"{Compact(gets)} / {Compact(hits)}";
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {storage,-17} | {traffic,-17} | {hit,5:F1}% | {st}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+-------------------+-------------------+--------+-------");
    }

    // BufferPool: SIZE | CAPACITY (F/T/I) | OPS (H/E/S) | USAGE % | MISS %
    private static void AppendBufferPoolTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("SIZE     | CAPACITY (F/T/I)         | OPS (H/E/S)         | USAGE % | MISS %");
        sb.AppendLine("---------+--------------------------+---------------------+---------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            long size   = Long(d, "BufferSize");
            long free   = Long(d, "Free");
            long total  = Long(d, "Total");
            long init   = Long(d, "Initial");
            long hits   = Long(d, "Hits");
            long exp    = Long(d, "Expands");
            long shr    = Long(d, "Shrinks");
            double usage = Dbl(d, "UsageRatio") * 100.0;
            double miss  = Dbl(d, "MissRate") * 100.0;
            string cap = $"{Compact(free)} / {Compact(total)} / {Compact(init)}";
            string ops = $"{Compact(hits)} / {Compact(exp)} / {Compact(shr)}";
            sb.Append(CultureInfo.InvariantCulture, $"{size,8} | {cap,-24} | {ops,-19} | {usage,6:F2}% | {miss:F2}%");
            sb.AppendLine();
        }
        sb.AppendLine("---------+--------------------------+---------------------+---------+-------");
    }

    // Recurring: NAME | RUNS (T/F) | RUN | SCHEDULE (L/N) | INTERVAL | TAG
    private static void AppendRecurringTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("NAME                         | RUNS (T/F)    | RUN | SCHEDULE (L/N)          | TAG");
        sb.AppendLine("-----------------------------+---------------+-----+-------------------------+----------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string nm   = Truncate(Str(d, "Name"), 28).PadRight(28);
            long runs   = Long(d, "TotalRuns");
            long fails  = Long(d, "ConsecutiveFailures");
            bool running = Bool(d, "IsRunning");
            string last = FmtTime(d, "LastRunUtc");
            string next = FmtTime(d, "NextRunUtc");
            string tag  = Str(d, "Tag");
            string rf   = $"{Compact(runs)} / {fails}";
            string sched = $"{last} / {next}";
            sb.Append(CultureInfo.InvariantCulture, $"{nm} | {rf,-13} | {(running ? "yes" : " no"),3} | {sched,-23} | {tag}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+---------------+-----+-------------------------+----------");
    }

    // Workers: ID | NAME | GROUP | AGE | PROGRESS
    private static void AppendWorkersTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("ID               | NAME                         | GROUP                        | PROGRESS");
        sb.AppendLine("-----------------+------------------------------+------------------------------+---------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string id   = Truncate(Str(d, "Id"), 16).PadRight(16);
            string name = Truncate(Str(d, "Name"), 28).PadRight(28);
            string grp  = Truncate(Str(d, "Group"), 28).PadRight(28);
            long prog   = Long(d, "Progress");
            sb.Append(CultureInfo.InvariantCulture, $"{id} | {name} | {grp} | {Compact(prog),8}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------+------------------------------+------------------------------+---------");
    }

    // Connections: ID | USERNAME | LEVEL | ALGO | UPTIME
    private static void AppendConnectionsTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("ID               | USERNAME         | LEVEL      | ALGORITHM  | UPTIME");
        sb.AppendLine("-----------------+------------------+------------+------------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string id   = Truncate(Str(d, "ID"), 16).PadRight(16);
            string user = Truncate(Str(d, "Username"), 16).PadRight(16);
            string lvl  = Truncate(Str(d, "Level"), 10).PadRight(10);
            string algo = Truncate(Str(d, "Algorithm"), 10).PadRight(10);
            long up     = Long(d, "UpTime");
            sb.Append(CultureInfo.InvariantCulture, $"{id} | {user} | {lvl} | {algo} | {up}s");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------+------------------+------------+------------+-------");
    }

    // Instances: TYPE | DISPOSABLE | SOURCE
    private static void AppendInstancesTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                                          | DISPOSABLE | SOURCE");
        sb.AppendLine("----------------------------------------------+------------+--------------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type = Truncate(Str(d, "Type"), 45).PadRight(45);
            bool disp   = Bool(d, "IsDisposable");
            string src  = Str(d, "Source");
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {(disp ? "Yes" : "No "),10} | {src}");
            sb.AppendLine();
        }
        sb.AppendLine("----------------------------------------------+------------+--------------");
    }

    // TopRecurringByFailures: NAME | FAILURES | LAST RUN | TAG
    private static void AppendTopRecurringTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("NAME                         | FAILURES | LAST RUN | TAG");
        sb.AppendLine("-----------------------------+----------+----------+----------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string nm   = Truncate(Str(d, "Name"), 28).PadRight(28);
            long fails  = Long(d, "ConsecutiveFailures");
            string last = FmtTime(d, "LastRunUtc");
            string tag  = Str(d, "Tag");
            sb.Append(CultureInfo.InvariantCulture, $"{nm} | {fails,8} | {last} | {tag}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+----------+----------+----------");
    }

    // UnhealthyPools: TYPE | FAILURES | LAST ACCESS | OUTSTANDING
    private static void AppendUnhealthyTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                         | FAILURES | LAST ACCESS      | OUTSTANDING");
        sb.AppendLine("-----------------------------+----------+------------------+------------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type = Truncate(Str(d, "Type"), 28).PadRight(28);
            long fails  = Long(d, "ConsecutiveFailures");
            string last = FmtTime(d, "LastAccessUtc");
            long out_   = Long(d, "Outstanding");
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {fails,8} | {last,16} | {out_,10}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+----------+------------------+------------");
    }

    // Fallback: show first 4 keys only
    private static void AppendFallbackTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        var first = list[0];
        string[] cols = new string[Math.Min(first.Count, 4)];
        int ci = 0;
        foreach (string k in first.Keys) { cols[ci++] = k; if (ci >= cols.Length) break; }

        // Header
        for (int c = 0; c < cols.Length; c++)
        {
            AppendPadded(sb, cols[c], 18);
            if (c < cols.Length - 1) sb.Append(" | ");
        }
        sb.AppendLine();
        for (int c = 0; c < cols.Length; c++)
        {
            sb.Append('-', 18);
            if (c < cols.Length - 1) sb.Append("-+-");
        }
        sb.AppendLine();

        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var row = list[r];
            for (int c = 0; c < cols.Length; c++)
            {
                string val = row.TryGetValue(cols[c], out object? v) ? FormatScalar(v) : "-";
                AppendPadded(sb, val, 18);
                if (c < cols.Length - 1) sb.Append(" | ");
            }
            sb.AppendLine();
        }
    }

    // ── Data extraction helpers (avoid exceptions on missing keys) ────────────

    private static string Str(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? (v?.ToString() ?? "-") : "-";

    private static long Long(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;

    private static double Dbl(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0.0;

    private static bool Bool(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) && v is bool b && b;

    private static string FmtTime(Dictionary<string, object> d, string k)
    {
        if (!d.TryGetValue(k, out object? v)) return "--:--:--";
        return v switch
        {
            DateTimeOffset dto => dto == DateTimeOffset.MinValue ? "--:--:--" : dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DateTime dt        => dt == DateTime.MinValue ? "--:--:--" : dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            _                  => "--:--:--"
        };
    }

    private static string Compact(long value)
    {
        if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 1_000_000) return $"{value / 1000.0:F1}k";
        return $"{value / 1_000_000.0:F1}M";
    }

    // ── Scalar formatting (zero-alloc where possible) ────────────────────────

    private static void AppendScalar(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:              sb.Append('-'); break;
            case string s:          sb.Append(s); break;
            case int i:             sb.Append(i); break;
            case long l:            sb.Append(l.ToString("N0", CultureInfo.InvariantCulture)); break;
            case double d:          sb.Append(d.ToString("F2", CultureInfo.InvariantCulture)); break;
            case float f:           sb.Append(f.ToString("F2", CultureInfo.InvariantCulture)); break;
            case bool b:            sb.Append(b ? "Yes" : "No"); break;
            case DateTime dt:       sb.Append(dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); break;
            case DateTimeOffset dto:sb.Append(dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); break;
            default:                sb.Append(value); break;
        }
    }

    private static string FormatScalar(object? value) => value switch
    {
        null                => "-",
        string s            => s,
        int i               => i.ToString(CultureInfo.InvariantCulture),
        long l              => l.ToString("N0", CultureInfo.InvariantCulture),
        double d            => d.ToString("F2", CultureInfo.InvariantCulture),
        float f             => f.ToString("F2", CultureInfo.InvariantCulture),
        bool b              => b ? "Yes" : "No",
        DateTime dt         => dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto  => dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _                   => value.ToString() ?? "-"
    };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : $"{s.AsSpan(0, max - 2)}..";


    // ── Inline helpers (avoid allocations) ────────────────────────────────────

    private static void AppendPad(StringBuilder sb, int indent)
    {
        for (int i = 0; i < indent * 2; i++) sb.Append(' ');
    }

    private static void AppendKeyColon(StringBuilder sb, string key, int indent)
    {
        int width = 34 - indent * 2;
        if (key.Length > width)
        {
            sb.Append(key.AsSpan(0, width - 1));
            sb.Append('…');
        }
        else
        {
            sb.Append(key);
            for (int i = key.Length; i < width; i++) sb.Append(' ');
        }
        sb.Append(':');
    }

    private static void AppendKV(StringBuilder sb, string key, object value)
    {
        sb.Append(key);
        sb.Append(": ");
        sb.Append(value);
        sb.AppendLine();
    }

    private static void AppendPadded(StringBuilder sb, string val, int width)
    {
        if (val.Length > width)
        {
            sb.Append(val.AsSpan(0, width - 2));
            sb.Append("..");
        }
        else
        {
            sb.Append(val);
            for (int i = val.Length; i < width; i++) sb.Append(' ');
        }
    }

    private static StringBuilder RentSb()
    {
        t_sb ??= new StringBuilder(4096);
        t_sb.Clear();
        return t_sb;
    }

    // ── Process page ─────────────────────────────────────────────────────────

    private static string FormatProcessData()
    {
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        proc.Refresh();
        ThreadPool.GetMaxThreads(out int maxW, out int maxIO);
        ThreadPool.GetAvailableThreads(out int freeW, out int freeIO);

        return $"""
            PID              : {proc.Id}
            Uptime           : {GetUptime()}
            Started          : {proc.StartTime:yyyy-MM-dd HH:mm:ss}
            
            Memory:
            Working Set      : {proc.WorkingSet64 / 1024 / 1024:F1} MB
            Private Bytes    : {proc.PrivateMemorySize64 / 1024 / 1024:F1} MB
            Virtual Memory   : {proc.VirtualMemorySize64 / 1024 / 1024:F1} MB
            GC Managed Heap  : {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB
            GC Gen0/1/2      : {GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}
            
            Threading:
            Proc Threads     : {proc.Threads.Count}
            ThreadPool W     : {maxW - freeW} / {maxW} active
            ThreadPool IO    : {maxIO - freeIO} / {maxIO} active
            Completed Items  : {ThreadPool.CompletedWorkItemCount:N0}
            CPU Time         : {proc.TotalProcessorTime.TotalSeconds:F2}s
            
            Runtime:
            Framework        : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}
            OS               : {System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim()}
            Arch             : {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}
            Processors       : {System.Environment.ProcessorCount}
            """;
    }

    // ── Snapshot (one-shot print) ─────────────────────────────────────────────

    private static void PrintAllPages(ConnectionHub hub)
    {
        foreach (Page p in Enum.GetValues<Page>())
        {
            AnsiConsole.Write(new Rule($"[grey]{PageLabels[(int)p]}[/]")
            {
                Justification = Justify.Left,
                Style         = s_greyDim
            });
            string text = FormatPageData(hub, p);
            AnsiConsole.MarkupLine($"[grey]{text.EscapeMarkup()}[/]");
            AnsiConsole.WriteLine();
        }
    }

    // ── Startup / Shutdown ────────────────────────────────────────────────────

    public static void PrintStartup(string endpoint)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Listening  [steelblue1]{endpoint}[/]   started {DateTime.Now:HH:mm:ss}[/]");
        AnsiConsole.MarkupLine("[grey dim]Select \"Live dashboard\" from the menu for real-time paged metrics.[/]");
        AnsiConsole.WriteLine();
    }

    public static void PrintShutdown()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Server stopped at {DateTime.Now:HH:mm:ss}[/]");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetUptime()
    {
        TimeSpan up = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
        return up.TotalHours >= 1
            ? $"{(int)up.TotalHours}h {up.Minutes}m {up.Seconds}s"
            : up.TotalMinutes >= 1
                ? $"{up.Minutes}m {up.Seconds}s"
                : $"{up.Seconds}s";
    }
}
