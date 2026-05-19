// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;

namespace Nalix.Framework.Memory.Internal.PoolTypes;

internal static class PoolTypeRegistry
{
    private static int s_typeCount;
    public static int NextId() => Interlocked.Increment(ref s_typeCount) - 1;
}
