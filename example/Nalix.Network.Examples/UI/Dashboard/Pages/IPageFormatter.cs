// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Connections;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>
/// Formats a single dashboard page from report data.
/// </summary>
internal interface IPageFormatter
{
    /// <summary>Human-readable label shown in the tab bar.</summary>
    string Label { get; }

    /// <summary>Generates the full text for this page.</summary>
    string Format(ConnectionHub hub);
}
