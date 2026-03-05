// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Enums;

/// <summary>
/// Chosen SIMD mode
/// </summary>
public enum SimdMode : System.Byte
{
    /// <summary>
    /// No SIMD
    /// </summary>
    NONE = 0,

    /// <summary>
    /// 128 bit SIMD
    /// </summary>
    V128 = 1,

    /// <summary>
    /// 256 bit SIMD
    /// </summary>
    V256 = 2,

    /// <summary>
    /// 512 bit SIMD
    /// </summary>
    V512 = 3,

    /// <summary>
    /// Autodetect
    /// </summary>
    AUTO_DETECT = 255,
}
