// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;

namespace Nalix.Framework.Memory.Internal.PoolTypes;

internal static class PoolType<T> where T : IPoolable
{
    public static readonly int Id = PoolTypeRegistry.NextId();
}
