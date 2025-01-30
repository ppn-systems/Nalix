using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using Notio.Database.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database;

public static class DatabaseContextExtensions
{
    public static async Task<object> CreateGroupChat(this NotioContext notioContext, string chatName, List<int> userIds)
    {
        if (userIds == null || userIds.Count < 2)
        {
            return new { Message = "A group chat must have at least two members." };
        }

        // Tạo một cuộc trò chuyện mới
        var newChat = new Chat
        {
            ChatName = chatName,
            ChatType = ChatType.Group,
            LastActivityAt = DateTime.UtcNow
        };

        // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu
        using (var transaction = await notioContext.Database.BeginTransactionAsync())
        {
            try
            {
                // Thêm chat vào cơ sở dữ liệu
                await notioContext.Chats.AddAsync(newChat);
                await notioContext.SaveChangesAsync();

                // Thêm người dùng vào cuộc trò chuyện
                var userChats = userIds.Select(userId => new UserChat
                {
                    UserId = userId,
                    ChatId = newChat.ChatId,
                    UserRole = UserRole.Member,
                    LastReadAt = DateTime.UtcNow
                }).ToList();

                await notioContext.UserChats.AddRangeAsync(userChats);
                await notioContext.SaveChangesAsync();

                // Commit giao dịch sau khi các thao tác thành công
                await transaction.CommitAsync();

                return new { Message = "Group chat created successfully", ChatId = newChat.ChatId };
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi xảy ra
                await transaction.RollbackAsync();
                return new { Message = $"Error occurred: {ex.Message}" };
            }
        }
    }

    public static async Task<object> SendMessageToGroup(this NotioContext notioContext, int chatId, int senderId, string content)
    {
        bool userInChat = await notioContext.UserChats
            .AnyAsync(uc => uc.ChatId == chatId && uc.UserId == senderId);

        if (!userInChat)
        {
            return new { Message = "User is not part of the chat" };
        }

        var newMessage = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            MessageType = MessageType.Text,
            CreatedAt = DateTime.UtcNow
        };

        await notioContext.Messages.AddAsync(newMessage);
        await notioContext.SaveChangesAsync();

        return new { Message = "Message sent successfully", newMessage.MessageId };
    }

    public async Task<object> SendMessageWithAttachment(
        this NotioContext notioContext, int chatId,
        int senderId, string content, string fileUrl,
        string fileName, string fileType, long fileSize)
    {
        bool userInChat = await notioContext.UserChats
            .AnyAsync(uc => uc.ChatId == chatId && uc.UserId == senderId);

        if (!userInChat)
        {
            return new { Message = "User is not part of the chat" };
        }

        Message newMessage = new()
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            MessageType = MessageType.Text,
            CreatedAt = DateTime.UtcNow
        };

        await notioContext.Messages.AddAsync(newMessage);
        await notioContext.SaveChangesAsync();

        MessageAttachment newAttachment = new()
        {
            MessageId = newMessage.MessageId,
            FileUrl = fileUrl,
            FileName = fileName,
            FileType = fileType,
            FileSize = fileSize,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MessageAttachments.AddAsync(newAttachment);
        await _context.SaveChangesAsync();

        return new { Message = "Message with attachment sent successfully", MessageId = newMessage.MessageId };
    }
}