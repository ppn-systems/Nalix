using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database.Extensions;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Get the last N messages in a chat.
    /// </summary>
    public static async Task<List<Message>> GetLastMessagesAsync(
        this DbSet<Message> messages, int chatId, int count = 10)
        => await messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync();

    /// <summary>
    /// Get all attachments of a message.
    /// </summary>
    public static async Task<List<MessageAttachment>> GetAttachmentsAsync(
        this DbSet<MessageAttachment> attachments, int messageId)
        => await attachments
            .Where(a => a.MessageId == messageId)
            .ToListAsync();

    /// <summary>
    /// Get unread messages for a user in a chat.
    /// </summary>
    public static async Task<List<Message>> GetUnreadMessagesAsync(this DbSet<Message> messages, int chatId, DateTime? lastReadAt)
        => await messages
            .Where(m => m.ChatId == chatId && m.CreatedAt > (lastReadAt ?? DateTime.MinValue))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    /// <summary>
    /// Get the last message in a chat.
    /// </summary>
    public static async Task<Message> GetLastMessageAsync(this DbSet<Message> messages, int chatId)
        => await messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Get messages by sender.
    /// </summary>
    public static async Task<List<Message>> GetMessagesBySenderAsync(this DbSet<Message> messages, int senderId)
        => await messages
            .Where(m => m.SenderId == senderId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
}