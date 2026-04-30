// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Formatting;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the InstanceManager page.</summary>
internal sealed class InstanceManagerPageFormatter : IPageFormatter
{
    public string Label => "Instances";

    public string Format(ConnectionHub hub)
        => ReportDataFormatter.Format(InstanceManager.Instance.GetReportData(), "InstanceManager");
}
