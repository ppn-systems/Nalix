// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Formatting;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the TaskManager page.</summary>
internal sealed class TaskManagerPageFormatter : IPageFormatter
{
    public string Label => "Tasks";

    public string Format(ConnectionHub hub)
    {
        TaskManager? mgr = InstanceManager.Instance.GetExistingInstance<TaskManager>();
        return mgr is null
            ? "(TaskManager not registered)"
            : ReportDataFormatter.Format(mgr.GetReportData(), "TaskManager");
    }
}
