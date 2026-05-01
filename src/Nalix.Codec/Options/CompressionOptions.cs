// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Configures when compression is enabled and when payloads are large enough
/// to justify the cost of compressing them.
/// </summary>
[IniComment("Compression configuration — controls when and how data is compressed")]
public sealed class CompressionOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets whether compression is enabled globally.
    /// </summary>
    [IniComment("Enable or disable compression (true = enabled, false = disabled)")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the minimum payload size, in bytes, required before compression is attempted.
    /// </summary>
    /// <remarks>
    /// Small payloads often grow after compression because of headers and framing,
    /// so this threshold prevents wasted CPU time on messages that are too small
    /// to benefit from compression.
    /// </remarks>
    [IniComment("Minimum data size (bytes) to trigger compression (e.g. 1024 = 1KB)")]
    public int MinSizeToCompress { get; init; } = 1024; // 1KB default

    /// <summary>
    /// Gets or sets the maximum allowed size, in bytes, for a decompressed packet payload.
    /// </summary>
    /// <remarks>
    /// This is a security limit used to prevent allocation-based DoS attacks (Zip Bombs).
    /// If a packet declares an original size larger than this limit, it will be rejected.
    /// </remarks>
    [IniComment("Maximum allowed size (bytes) for a decompressed packet payload (default 32MB)")]
    public int MaxDecompressedSize { get; init; } = 32 * 1024 * 1024; // 32MB default

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <remarks>
    /// This relies on data annotation validation so callers can reuse the same
    /// validation path as the rest of the configuration system.
    /// </remarks>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        if (this.MinSizeToCompress <= 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MinSizeToCompress must be greater than 0.");
        }

        if (this.MaxDecompressedSize < 1024)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxDecompressedSize must be at least 1024 bytes.");
        }
    }
}
