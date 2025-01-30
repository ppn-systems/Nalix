using Notio.Database.Enums;
using System;
using System.Collections.Generic;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho cuộc trò chuyện trong cơ sở dữ liệu.
/// </summary>
public class Chat : BaseEntity
{
    public int ChatId { get; set; }
    public string ChatName { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public ChatType ChatType { get; set; }

    // Navigation properties
    public virtual ICollection<UserChat> UserChats { get; set; }

    public virtual ICollection<Message> Messages { get; set; }
}