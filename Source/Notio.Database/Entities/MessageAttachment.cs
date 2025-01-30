namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho file đính kèm trong tin nhắn trong cơ sở dữ liệu.
/// </summary>
public class MessageAttachment : BaseEntity
{
    public int AttachmentId { get; set; }
    public int MessageId { get; set; }
    public string FileUrl { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public long FileSize { get; set; }

    // Navigation property
    public virtual Message Message { get; set; }
}