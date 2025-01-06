namespace Notio.Logging.Enums;

/// <summary>
/// Đại diện cho các mức độ nghiêm trọng của một thông điệp nhật ký.
/// </summary>
public enum LoggingLevel
{
    /// <summary>
    /// Dùng để đại diện cho các thông điệp ở mức theo dõi (trace-level).
    /// </summary>
    Trace,

    /// <summary>
    /// Dùng để đại diện cho các thông điệp ở mức gỡ lỗi (debug-level).
    /// </summary>
    Debug,

    /// <summary>
    /// Dùng để đại diện cho các thông điệp mang tính thông tin (informational).
    /// </summary>
    Information,

    /// <summary>
    /// Dùng để đại diện cho các thông điệp ở mức cảnh báo (warning-level).
    /// </summary>
    Warning,

    /// <summary>
    /// Dùng để đại diện cho các thông điệp ở mức lỗi (error-level).
    /// </summary>
    Error,

    /// <summary>
    /// Dùng để đại diện cho các thông điệp ở mức nghiêm trọng (critical-level).
    /// </summary>
    Critical,

    /// <summary>
    /// Đại diện cho mức độ không cụ thể hoặc không xác định (non-specific).
    /// </summary>
    None
}