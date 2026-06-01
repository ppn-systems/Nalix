// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Observability.Contracts;

/// <summary>
/// Defines the lifecycle stage of a runtime observation packet.
/// </summary>
public enum RuntimeObservationStage : byte
{
    /// <summary>
    /// No observation operation has been assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// The packet requests runtime observation data.
    /// </summary>
    REQUEST = 0x01,

    /// <summary>
    /// The packet contains runtime observation data or a failure reason.
    /// </summary>
    RESPONSE = 0x02
}
