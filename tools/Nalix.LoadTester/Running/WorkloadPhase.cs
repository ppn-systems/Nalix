// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Running;

internal enum WorkloadPhase
{
    RampUp,
    Warmup,
    Steady,
    Cooldown,
    Completed
}
