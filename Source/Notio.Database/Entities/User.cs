using System.Collections.Generic;

namespace Notio.Database.Entities;

/// <summary>
/// Đại diện cho người dùng trong cơ sở dữ liệu.
/// </summary>
public class User : BaseEntity
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public string AvatarUrl { get; set; }

    // Navigation properties - sử dụng lazy loading
    public virtual ICollection<UserChat> UserChats { get; set; }

    public virtual ICollection<Message> SentMessages { get; set; }
}