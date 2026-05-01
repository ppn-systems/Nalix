// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Examples.UI.Formatting;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the BufferPoolManager page.</summary>
internal sealed class BufferPoolPageFormatter : IPageFormatter
{
    public string Label => "Buf Pool";

    public string Format(ConnectionHub hub)
    {
        BufferPoolManager? mgr = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();
        return mgr is null
            ? "(BufferPoolManager not registered)"
            : ReportDataFormatter.Format(mgr.GetReportData(), "BufferPoolManager");
    }
}
