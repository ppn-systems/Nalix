using System;
using System.Collections.Generic;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho tin nhắn trong cơ sở dữ liệu.
/// </summary>
public class Message
{
    public long MessageId { get; set; }
    public long ChatId { get; set; }
    public long SenderId { get; set; }
    public string MessageType { get; set; }
    public string MessageContent { get; set; }
    public string MediaMetadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public User Sender { get; set; }

    public Chat Chat { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; }
}