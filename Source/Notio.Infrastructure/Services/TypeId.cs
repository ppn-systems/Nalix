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
    /// ID phiên bản khu vực.
    /// </summary>
    Region = 1,

    /// <summary>
    /// ID tài khoản hoặc người chơi trong cơ sở dữ liệu.
    /// </summary>
    Account = 2,

    /// <summary>
    /// ID kết nối khách hàng.
    /// </summary>
    Session = 3,

    /// <summary>
    /// ID phiên bản trò chơi.
    /// </summary>
    Game = 4,

    /// <summary>
    /// ID đại diện cho các thực thể (hình đại diện, vật phẩm, v.v.).
    /// </summary>
    Entity = 5,

    /// <summary>
    /// Giới hạn loại ID, không được vượt qua giá trị này.
    /// </summary>
    Limit = 1 << 4
}