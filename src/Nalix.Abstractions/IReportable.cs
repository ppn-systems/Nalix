// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    IDictionary<string, object> GetReportData();
}
