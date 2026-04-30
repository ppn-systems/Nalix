// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Spectre.Console;

namespace Nalix.SDK.Client.UI;

/// <summary>Color palette used across the SDK client TUI.</summary>
internal static class Theme
{
    public static readonly Style Title    = new(foreground: Color.Aqua,    decoration: Decoration.Bold);
    public static readonly Style Success  = new(foreground: Color.Green,   decoration: Decoration.Bold);
    public static readonly Style Warning  = new(foreground: Color.Yellow);
    public static readonly Style Error    = new(foreground: Color.Red,     decoration: Decoration.Bold);
    public static readonly Style Info     = new(foreground: Color.SteelBlue1);
    public static readonly Style Muted    = new(foreground: Color.Grey);
    public static readonly Style Accent   = new(foreground: Color.MediumPurple1, decoration: Decoration.Bold);
    public static readonly Style Ping     = new(foreground: Color.Chartreuse1, decoration: Decoration.Bold);
    public static readonly Style PingWarn = new(foreground: Color.Gold1);
    public static readonly Style PingBad  = new(foreground: Color.OrangeRed1, decoration: Decoration.Bold);

    /// <summary>Returns a color-coded style based on the ping RTT value (ms).</summary>
    public static Style PingStyle(double ms) => ms switch
    {
        < 50  => Ping,
        < 120 => PingWarn,
        _     => PingBad
    };
}
