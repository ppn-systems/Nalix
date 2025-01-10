namespace Notio.Common.Database;

/// <summary>
/// Loại tin nhắn.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Tin nhắn dạng văn bản.
    /// </summary>
    Text,

    /// <summary>
    /// Tin nhắn dạng hình ảnh.
    /// </summary>
    Image,

    /// <summary>
    /// Tin nhắn dạng video.
    /// </summary>
    Video,

    /// <summary>
    /// Tin nhắn dạng tệp tin.
    /// </summary>
    File
}