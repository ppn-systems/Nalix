// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Common.Enums;

/// <summary>
/// Defines the category of an <see cref="ISnowflake"/> value,  
/// used to distinguish between different purposes or entities in the system.
/// </summary>
public enum SnowflakeType : System.Byte
{
    #region Core System

    /// <summary>
    /// NONE or generic purpose.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// For system configurations or version tracking.
    /// </summary>
    Configuration = 1,

    /// <summary>
    /// For logging events and audit trails.
    /// </summary>
    Log = 2,

    /// <summary>
    /// SYSTEM-wide unique identifier.
    /// </summary>
    System = 3,

    #endregion Core System

    #region User & Security

    /// <summary>
    /// For user account management.
    /// </summary>
    Account = 10,

    /// <summary>
    /// For session management.
    /// </summary>
    Session = 11,

    #endregion User & Security

    #region Communication & Messaging

    /// <summary>
    /// For chat and message management.
    /// </summary>
    Message = 20,

    /// <summary>
    /// For notifications and alerts.
    /// </summary>
    Notification = 21,

    /// <summary>
    /// For email tracking.
    /// </summary>
    Email = 22,

    /// <summary>
    /// For phone verification and SMS tracking.
    /// </summary>
    Sms = 23,

    #endregion Communication & Messaging

    #region Business & Transactions

    /// <summary>
    /// For tracking orders and purchases.
    /// </summary>
    Order = 30,

    /// <summary>
    /// For inventory management.
    /// </summary>
    Inventory = 31,

    /// <summary>
    /// For financial transactions.
    /// </summary>
    Transaction = 32,

    /// <summary>
    /// For invoice and billing.
    /// </summary>
    Invoice = 33,

    /// <summary>
    /// For tracking customer support tickets.
    /// </summary>
    SupportTicket = 34,

    #endregion Business & Transactions

    /// <summary>
    /// The maximum valid value for <see cref="SnowflakeType"/>.
    /// </summary>
    MaxValue = 255
}
