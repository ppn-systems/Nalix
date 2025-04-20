namespace Notio.Common.Identity;

/// <summary>
/// Number type to serve different purposes in the system.
/// </summary>
public enum IdentifierType : byte
{
    #region Core System

    /// <summary>
    /// Unknown or generic purpose.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// For system configurations or versions.
    /// </summary>
    Configuration = 1,

    /// <summary>
    /// For logging events and audit trails.
    /// </summary>
    Log = 2,

    /// <summary>
    /// For tracking API keys and authentication tokens.
    /// </summary>
    ApiKey = 3,

    /// <summary>
    /// For network communication packets.
    /// </summary>
    Packet = 4,

    #endregion

    #region User & Security

    /// <summary>
    /// For user account management.
    /// </summary>
    Account = 10,

    /// <summary>
    /// For session management.
    /// </summary>
    Session = 11,

    /// <summary>
    /// For authentication-related tokens.
    /// </summary>
    AuthToken = 12,

    /// <summary>
    /// For permissions and role-based access control (RBAC).
    /// </summary>
    Permission = 13,

    #endregion

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

    #endregion

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

    #endregion

    #region External Integration

    /// <summary>
    /// For third-party API integration.
    /// </summary>
    ExternalApi = 40,

    /// <summary>
    /// For OAuth and social login connections.
    /// </summary>
    SocialLogin = 41,

    /// <summary>
    /// For tracking webhooks.
    /// </summary>
    Webhook = 42,

    #endregion

    /// <summary>
    /// The maximum valid value for Number types.
    /// </summary>
    MaxValue = 255
}
