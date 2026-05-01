// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Formatting;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the ObjectPoolManager page.</summary>
internal sealed class ObjectPoolPageFormatter : IPageFormatter
{
    public string Label => "Obj Pool";

    public string Format(ConnectionHub hub)
    {
        ObjectPoolManager? mgr = InstanceManager.Instance.GetExistingInstance<ObjectPoolManager>();
        return mgr is null
            ? "(ObjectPoolManager not registered)"
            : ReportDataFormatter.Format(mgr.GetReportData(), "ObjectPoolManager");
    }
}
