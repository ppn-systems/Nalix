// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Spectre.Console;

namespace Nalix.Examples.SDK.UI;

/// <summary>Renders the ASCII art banner header.</summary>
internal static class Banner
{
    private const string AsciiArt = """
    ███╗   ██╗ █████╗ ██╗     ██╗██╗  ██╗    ███████╗██████╗ ██╗  ██╗
    ████╗  ██║██╔══██╗██║     ██║╚██╗██╔╝    ██╔════╝██╔══██╗██║ ██╔╝
    ██╔██╗ ██║███████║██║     ██║ ╚███╔╝     ███████╗██║  ██║█████╔╝ 
    ██║╚██╗██║██╔══██║██║     ██║ ██╔██╗     ╚════██║██║  ██║██╔═██╗ 
    ██║ ╚████║██║  ██║███████╗██║██╔╝ ██╗    ███████║██████╔╝██║  ██╗
    ╚═╝  ╚═══╝╚═╝  ╚═╝╚══════╝╚═╝╚═╝  ╚═╝   ╚══════╝╚═════╝ ╚═╝  ╚═╝
    """;

    public static void Render()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Markup($"[aqua bold]{AsciiArt.EscapeMarkup()}[/]"));
        AnsiConsole.Write(new Rule("[grey]Nalix SDK Interactive Client v1.0[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("grey dim")
        });
        AnsiConsole.WriteLine();
    }
}
