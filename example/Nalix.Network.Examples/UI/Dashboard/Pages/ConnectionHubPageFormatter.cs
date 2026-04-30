// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Formatting;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the ConnectionHub page.</summary>
internal sealed class ConnectionHubPageFormatter : IPageFormatter
{
    public string Label => "Conn Hub";

    public string Format(ConnectionHub hub)
        => ReportDataFormatter.Format(hub.GetReportData(), "ConnectionHub");
}
