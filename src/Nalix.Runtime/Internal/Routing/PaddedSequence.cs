// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Nalix.Runtime.Internal.Routing;

/// <summary>
/// Cache-line padded 64-bit counter used to reduce false sharing on hot atomic counters.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct PaddedSequence
{
    [FieldOffset(64)] public long Value;
}
