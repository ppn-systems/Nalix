// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Abstractions;

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
