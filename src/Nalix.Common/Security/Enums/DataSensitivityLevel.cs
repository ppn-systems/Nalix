// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Security.Enums;

/// <summary>
/// Sensitivity classification for data protection compliance (GDPR, HIPAA, etc.).
/// </summary>
public enum DataSensitivityLevel : System.Byte
{
    /// <summary>
    /// Public data - no encryption required.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Internal data - optional encryption.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Confidential data - encryption recommended.
    /// </summary>
    Confidential = 2,

    /// <summary>
    /// Highly sensitive data - encryption mandatory (PII, PHI, financial).
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical data - encryption + auditing required (passwords, secrets).
    /// </summary>
    Critical = 4
}
