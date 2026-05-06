// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace Nalix.Abstractions;

/// <summary>
/// Defines a contract for managers that can generate reports.
/// </summary>
public interface IReportable
{
    /// <summary>
    /// Generates a human-readable report about the current state.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    string GenerateReport();

    /// <summary>
    /// Generates report data as key-value pairs describing the current state.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    [Obsolete("Use WriteReportData(System.Text.Json.Utf8JsonWriter) for more efficient JSON output instead.")]
    IDictionary<string, object> GetReportData();

#if NET10_0_OR_GREATER
    /// <summary>
    /// Writes report data directly to a <see cref="System.Text.Json.Utf8JsonWriter"/> for zero-allocation JSON output.
    /// Override this method in implementations to avoid dictionary allocations and boxing.
    /// </summary>
    /// <param name="writer">The JSON writer to write report data to.</param>
    void WriteReportData(System.Text.Json.Utf8JsonWriter writer);
#endif
}
