// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Observability.Contracts;

public enum RuntimeObservationStage : byte
{
    NONE = 0x00,
    REQUEST = 0x01,
    RESPONSE = 0x02
}
