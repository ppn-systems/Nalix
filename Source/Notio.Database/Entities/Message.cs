using Notio.Database.Enums;
using System.Collections.Generic;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho tin nhắn trong cơ sở dữ liệu.
/// </summary>
public class Message : BaseEntity
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int ChatId { get; set; }
    public string Content { get; set; } = null!;
    public MessageType MessageType { get; set; }
    public bool IsEdited { get; set; }

    // Navigation properties
    public virtual User Sender { get; set; }

    public virtual Chat Chat { get; set; }
    public virtual ICollection<MessageAttachment> Attachments { get; set; }
}