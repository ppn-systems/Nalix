// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a contract for managers that can generate reports.
/// </summary>
public interface IReportable
{
    /// <summary>
    /// Generates a human-readable report about the current state.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    System.String GenerateReport();
}
