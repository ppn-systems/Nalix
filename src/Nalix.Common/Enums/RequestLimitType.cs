// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Defines request rate limit levels for firewall or throttling configurations.
/// Each level sets a threshold for the number of requests allowed.
/// </summary>
public enum RequestLimitType : System.Byte
{
    /// <summary>
    /// Low request limit — allows only a small number of requests.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium request limit — allows a moderate number of requests.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High request limit — allows a large number of requests.
    /// </summary>
    High = 3,

    /// <summary>
    /// Login-specific request limit — applies rate limiting rules for login attempts.
    /// </summary>
    Login = 4
}
