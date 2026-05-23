// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Observability.Contracts;

public enum RuntimeObservationTarget : byte
{
    NONE = 0x00,
    DISPATCH = 0x01,
    TASKS = 0x02,
    BUFFERS = 0x03,
    CONNECTIONS = 0x04,
    INSTANCES = 0x05,
    OBJECT_POOLS = 0x06,
    CONNECTION_GUARD = 0x07
}
