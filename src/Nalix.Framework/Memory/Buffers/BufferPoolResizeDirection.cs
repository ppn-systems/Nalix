// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Framework.Memory.Buffers;

/// <summary>
/// Specifies the direction of a buffer pool resize operation.
/// </summary>
public enum BufferPoolResizeDirection : byte
{
    /// <summary>The pool is expanding to handle more demand.</summary>
    Increase,

    /// <summary>The pool is shrinking to release memory.</summary>
    Decrease
}
