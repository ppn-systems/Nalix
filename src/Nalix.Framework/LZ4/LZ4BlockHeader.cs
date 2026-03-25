// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nalix.Framework.LZ4;

/// <summary>
/// Defines the block header structure used in Nalix compression, which contains metadata
/// about the original and compressed data lengths.
/// </summary>
/// <remarks>
/// The header is a fixed-size structure that precedes the compressed data. It contains
/// information necessary to properly decompress the data, such as the original data length
/// and the total compressed length (including the header size).
/// </remarks>
/// <param name="originalLength">The length of the original data before compression.</param>
/// <param name="compressedLength">The total length of the compressed data, including the header.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[DebuggerDisplay("OrigLen={OriginalLength}, CompLen={CompressedLength}")]
public readonly struct LZ4BlockHeader(int originalLength, int compressedLength)
{
    /// <summary>
    /// The size of the header structure in bytes. This is the sum of the sizes of the two integer fields.
    /// </summary>
    public const int Size = sizeof(int) * 2; // 8 bytes

    /// <summary>
    /// Gets the original length of the data before compression.
    /// </summary>
    public readonly int OriginalLength = originalLength;

    /// <summary>
    /// Gets the total length of the compressed data, including the size of the header.
    /// </summary>
    public readonly int CompressedLength = compressedLength;
}
