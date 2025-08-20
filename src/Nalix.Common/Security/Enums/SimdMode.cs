// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Security.Enums;

/// <summary>
/// Chosen SIMD mode
/// </summary>
public enum SimdMode : System.Byte
{
    /// <summary>
    /// No SIMD
    /// </summary>
    None = 0,

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
    AutoDetect = 255,
}
