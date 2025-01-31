using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using Notio.Database.Enums;
using System;
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

    /// <summary>
    /// Create a new chat and add users to it.
    /// </summary>
    public static async Task<Chat> CreateChatAsync(
        this DbSet<Chat> chats,
        DbSet<UserChat> userChats,
        string chatName,
        ChatType chatType,
        List<int> userIds,
        int createdByUserId,
        DbContext context)
    {
        if (userIds == null || userIds.Count == 0)
        {
            throw new ArgumentException("At least one user must be added to the chat.");
        }

        var chat = new Chat
        {
            ChatName = chatName,
            ChatType = chatType,
            LastActivityAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await chats.AddAsync(chat);
        await context.SaveChangesAsync();

        var userChatList = userIds.Select(userId => new UserChat
        {
            ChatId = chat.ChatId,
            UserId = userId,
            UserRole = userId == createdByUserId ? UserRole.Admin : UserRole.Member,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await userChats.AddRangeAsync(userChatList);
        await context.SaveChangesAsync();

        return chat;
    }

    // <summary>
    /// Get chats for a user with the last message in each chat.
    /// </summary>
    public static async Task<List<dynamic>> GetUserChatsWithLastMessageAsync(
        this DbSet<UserChat> userChats, DbSet<Message> messages, int userId)
        => await userChats
            .Where(uc => uc.UserId == userId)
            .Select(uc => new
            {
                uc.ChatId,
                uc.Chat.ChatName,
                LastMessage = messages
                    .Where(m => m.ChatId == uc.ChatId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Content,
                        m.CreatedAt
                    })
                    .FirstOrDefault()
            })
            .ToListAsync<dynamic>();
}