using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database.Extensions;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Get active chats ordered by last activity.
    /// </summary>
    public static async Task<List<Chat>> GetRecentChatsAsync(this DbSet<Chat> chats)
        => await chats.Where(c => !c.IsDeleted)
                      .OrderByDescending(c => c.LastActivityAt)
                      .ToListAsync();

    /// <summary>
    /// Get a chat by ID with its messages.
    /// </summary>
    public static async Task<Chat> GetChatWithMessagesAsync(this DbSet<Chat> chats, int chatId)
        => await chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.ChatId == chatId);

    /// <summary>
    /// Check if a chat exists.
    /// </summary>
    public static async Task<bool> ChatExistsAsync(this DbSet<Chat> chats, int chatId)
        => await chats.AnyAsync(c => c.ChatId == chatId);

    /// <summary>
    /// Get the most active chats (sorted by last activity).
    /// </summary>
    public static async Task<List<Chat>> GetMostActiveChatsAsync(this DbSet<Chat> chats, int count)
        => await chats
            .OrderByDescending(c => c.LastActivityAt)
            .Take(count)
            .ToListAsync();

    /// <summary>
    /// Get all users in a chat.
    /// </summary>
    public static async Task<List<User>> GetChatUsersAsync(this DbSet<UserChat> userChats, int chatId)
        => await userChats
            .Where(uc => uc.ChatId == chatId)
            .Select(uc => uc.User)
            .ToListAsync();
}