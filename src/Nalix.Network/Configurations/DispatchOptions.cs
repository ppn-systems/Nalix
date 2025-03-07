// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Enums;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Options for dispatch channels (per-connection queue bound and drop behavior).
/// </summary>
public sealed class DispatchOptions : ConfigurationLoader
{
    /// <summary>
    /// Max items allowed in a single connection queue.
    /// Set to 0 or negative to disable bounding.
    /// </summary>
    public System.Int32 MaxPerConnectionQueue { get; init; } = 0;

    /// <summary>
    /// Drop strategy when the per-connection queue is full.
    /// </summary>
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DROP_NEWEST;
}
