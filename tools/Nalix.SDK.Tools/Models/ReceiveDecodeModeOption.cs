// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents one selectable receive decode mode.
/// </summary>
public sealed class ReceiveDecodeModeOption
{
    /// <summary>
    /// Gets or sets the decode mode.
    /// </summary>
    public required PacketReceiveDecodeMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string DisplayName { get; init; }
}
