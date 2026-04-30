// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Dashboard;
using Nalix.Network.Examples.UI.Dashboard.Pages;
using Spectre.Console;

namespace Nalix.Network.Examples.UI;

/// <summary>
/// Thin orchestrator — owns the menu loop and delegates rendering
/// to <see cref="DashboardRenderer"/>.
/// </summary>
internal static class ServerConsole
{
    // ── Menu labels ──────────────────────────────────────────────────────────
    private const string M_DASHBOARD = "Live dashboard (paged, real-time)";
    private const string M_SNAPSHOT  = "Snapshot (one-shot print)";
    private const string M_EXIT      = "Stop server";

    private static readonly Style s_highlight = Style.Parse("steelblue1 bold");

    // ── Startup / Shutdown banners ────────────────────────────────────────────

    public static void PrintStartup(string endpoint)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[aqua bold]Nalix Server Online — {endpoint}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("grey dim")
        });
        AnsiConsole.MarkupLine("  [grey dim]Press Q inside the dashboard to return to this menu.[/]");
    }

    public static void PrintShutdown()
    {
        AnsiConsole.Write(new Rule("[grey]Server stopped[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("grey dim")
        });
    }

    // ── Menu loop ────────────────────────────────────────────────────────────

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
                case M_DASHBOARD:
                    await RunDashboardAsync(hub, shutdown.Token).ConfigureAwait(false);
                    break;

                case M_SNAPSHOT:
                    PrintSnapshot(hub);
                    break;

                case M_EXIT:
                    shutdown.Cancel();
                    break;
            }
        }
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    private static async Task RunDashboardAsync(ConnectionHub hub, CancellationToken ct)
    {
        IPageFormatter[] pages =
        [
            new ProcessPageFormatter(),
            new ConnectionHubPageFormatter(),
            new ObjectPoolPageFormatter(),
            new BufferPoolPageFormatter(),
            new TaskManagerPageFormatter(),
            new InstanceManagerPageFormatter()
        ];

        var renderer = new DashboardRenderer(hub, pages);
        await renderer.RunAsync(ct).ConfigureAwait(false);
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────

    private static void PrintSnapshot(ConnectionHub hub)
    {
        PrintSection("ConnectionHub", hub.GenerateReport());

        var pool = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>();
        if (pool is not null) PrintSection("ObjectPoolManager", pool.GenerateReport());

        var buf = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();
        if (buf is not null) PrintSection("BufferPoolManager", buf.GenerateReport());

        var tasks = InstanceManager.Instance.GetExistingInstance<TaskManager>();
        if (tasks is not null) PrintSection("TaskManager", tasks.GenerateReport());

        PrintSection("InstanceManager", InstanceManager.Instance.GenerateReport());
    }

    private static void PrintSection(string title, string content)
    {
        AnsiConsole.Write(new Panel(new Markup($"[grey]{content.EscapeMarkup()}[/]"))
        {
            Header      = new PanelHeader($" [steelblue1 bold]{title}[/] "),
            Border      = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            Padding     = new Padding(1, 0),
            Expand      = true
        });
        AnsiConsole.WriteLine();
    }
}
