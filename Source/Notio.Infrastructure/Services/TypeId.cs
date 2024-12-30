namespace Notio.Infrastructure.Services;

/// <summary>
/// Loại ID để phục vụ cho các mục đích khác nhau trong hệ thống.
/// </summary>
public enum TypeId
{
    /// <summary>
    /// ID chung không có mục đích cụ thể.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// ID phiên bản hệ thống hoặc cấu hình chung.
    /// </summary>
    System = 1,

    /// <summary>
    /// ID tài khoản hoặc người chơi trong cơ sở dữ liệu.
    /// </summary>
    Account = 2,

    /// <summary>
    /// ID kết nối khách hàng.
    /// </summary>
    Session = 3,

    /// <summary>
    /// ID dành cho các đoạn chat.
    /// </summary>
    Chat = 4,

    /// <summary>
    /// Giới hạn loại ID, không được vượt qua giá trị này.
    /// </summary>
    Limit = 1 << 5
}