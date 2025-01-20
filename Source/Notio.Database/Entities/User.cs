using System;
using System.Collections.Generic;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho người dùng trong cơ sở dữ liệu.
/// </summary>
public class User
{
    public long UserId { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public string AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; }

    // Navigation properties
    public ICollection<UserChat> UserChats { get; set; }

    public ICollection<Message> SentMessages { get; set; }
}