namespace Notio.Shared.Identification;

/// <summary>
/// Loại ID để phục vụ cho các mục đích khác nhau trong hệ thống.
/// </summary>
public enum TypeId
{
    /// <summary>
    /// Không có mục đích cụ thể.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Dành cho các cấu hình hoặc phiên bản hệ thống.
    /// </summary>
    System = 1,

    /// <summary>
    /// Dành cho quản lý tài khoản người dùng.
    /// </summary>
    Account = 2,

    /// <summary>
    /// Dành cho quản lý phiên.
    /// </summary>
    Session = 3,

    /// <summary>
    /// Dành cho quản lý các đoạn chat, tin nhắn.
    /// </summary>
    Chat = 4,

    /// <summary>
    /// Dành cho các gói tin giao tiếp trong mạng lưới.
    /// </summary>
    Packet = 5,

    /// <summary>
    /// Giới hạn về số loại ID, không vượt quá giá trị này.
    /// </summary>
    Limit = 1 << 5
}