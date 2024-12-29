using System;

namespace Notio.Common.Models.Db;

/// <summary>
/// Đại diện cho mối quan hệ giữa người dùng và cuộc trò chuyện trong cơ sở dữ liệu.
/// </summary>
public class UserChat
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public string UserRole { get; set; }
    public DateTime JoinedAt { get; set; }
    public long? LastReadMessageId { get; set; }

    // Navigation properties
    public User User { get; set; }

    public Chat Chat { get; set; }
}