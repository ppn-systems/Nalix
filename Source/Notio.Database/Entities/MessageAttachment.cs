using System;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho file đính kèm trong tin nhắn trong cơ sở dữ liệu.
/// </summary>
public class MessageAttachment
{
    public long AttachmentId { get; set; }
    public long MessageId { get; set; }
    public string FileUrl { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public string FileType { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public Message Message { get; set; }
}