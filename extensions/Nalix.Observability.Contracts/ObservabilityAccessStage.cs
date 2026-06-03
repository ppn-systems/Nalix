// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Observability.Contracts;

/// <summary>
/// Defines the lifecycle stage of an observability access packet.
/// </summary>
public enum ObservabilityAccessStage : byte
{
    /// <summary>
    /// No access operation has been assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// The packet requests administrative observability access.
    /// </summary>
    REQUEST = 0x01,

    /// <summary>
    /// The packet contains the result of an access request.
    /// </summary>
    RESPONSE = 0x02
}
