namespace Notio.Network.Firewall.Enums;

/// <summary>
/// Represents different levels of connection limits.
/// </summary>
public enum ConnectionLimit
{
    /// <summary>
    /// Represents a low number of simultaneous connections.
    /// </summary>
    Low,  // Ít kết nối đồng thời

    /// <summary>
    /// Represents a medium number of simultaneous connections.
    /// </summary>
    Medium,  // Số lượng kết nối trung bình

    /// <summary>
    /// Represents a high number of simultaneous connections.
    /// </summary>
    High,  // Nhiều kết nối đồng thời

    /// <summary>
    /// Represents unlimited simultaneous connections.
    /// </summary>
    Unlimited  // Không giới hạn kết nối
}
