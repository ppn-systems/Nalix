using Notio.Database.Enums;
using System;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho mối quan hệ giữa người dùng và cuộc trò chuyện trong cơ sở dữ liệu.
/// </summary>
public class UserChat : BaseEntity
{
    public int UserId { get; set; }
    public int ChatId { get; set; }
    public UserRole UserRole { get; set; }
    public bool IsMuted { get; set; }
    public DateTime? LastReadAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; }

    public virtual Chat Chat { get; set; }
}