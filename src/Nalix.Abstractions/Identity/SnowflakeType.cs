// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Identity;

/// <summary>
/// Categorizes a snowflake so the system can tell what kind of entity it refers to.
/// </summary>
public enum SnowflakeType : byte
{
    #region Core System

    /// <summary>
    /// Unspecified or generic purpose.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Configuration and versioning identifiers.
    /// </summary>
    Configuration = 1,

    /// <summary>
    /// Logging and audit trail identifiers.
    /// </summary>
    Log = 2,

    /// <summary>
    /// System-wide infrastructure identifiers.
    /// </summary>
    System = 3,

    #endregion Core System

    #region User & Security

    /// <summary>
    /// User account identifiers.
    /// </summary>
    Account = 10,

    /// <summary>
    /// Session identifiers.
    /// </summary>
    Session = 11,

    #endregion User & Security

    #region Communication & Messaging

    /// <summary>
    /// Messaging identifiers.
    /// </summary>
    Message = 20,

    /// <summary>
    /// Notification identifiers.
    /// </summary>
    Notification = 21,

    /// <summary>
    /// Email identifiers.
    /// </summary>
    Email = 22,

    /// <summary>
    /// SMS and phone verification identifiers.
    /// </summary>
    Sms = 23,

    #endregion Communication & Messaging

    #region Business & Transactions

    /// <summary>
    /// Order identifiers.
    /// </summary>
    Order = 30,

    /// <summary>
    /// Inventory identifiers.
    /// </summary>
    Inventory = 31,

    /// <summary>
    /// Transaction identifiers.
    /// </summary>
    Transaction = 32,

    /// <summary>
    /// Invoice and billing identifiers.
    /// </summary>
    Invoice = 33,

    /// <summary>
    /// Support ticket identifiers.
    /// </summary>
    SupportTicket = 34,

    #endregion Business & Transactions

    /// <summary>
    /// The maximum numeric value reserved for the enum.
    /// </summary>
    MaxValue = 255
}
