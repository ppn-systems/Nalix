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
    public static async Task<object> EditMessage(
        this NotioContext notioContext, int messageId,
        int senderId, string newContent)
    {
        var message = await notioContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId && m.SenderId == senderId);

        if (message == null)
        {
            return new { Message = "Message not found or user is not the sender" };
        }

        message.Content = newContent;
        message.IsEdited = true;
        message.UpdatedAt = DateTime.UtcNow;

        await notioContext.SaveChangesAsync();

        return new { Message = "Message updated successfully" };
    }

    // Xóa tin nhắn
    public static async Task<object> DeleteMessage(
        this NotioContext notioContext, int messageId, int senderId)
    {
        var message = await notioContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId && m.SenderId == senderId);

        if (message == null)
        {
            return new { Message = "Message not found or user is not the sender" };
        }

        message.IsDeleted = true;
        await notioContext.SaveChangesAsync();

        return new { Message = "Message deleted successfully" };
    }

    // Xóa cuộc trò chuyện (đánh dấu là xóa)
    public static async Task<object> DeleteChat(
        this NotioContext notioContext, int chatId, int userId)
    {
        var userChat = await notioContext.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        if (userChat == null)
        {
            return new { Message = "User is not part of the chat" };
        }

        // Đánh dấu cuộc trò chuyện là đã xóa
        var chat = await notioContext.Chats.FindAsync(chatId);
        if (chat != null)
        {
            chat.IsDeleted = true;
            await notioContext.SaveChangesAsync();

            return new { Message = "Chat deleted successfully" };
        }

        return new { Message = "Chat not found" };
    }

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
        using var transaction = await notioContext.Database.BeginTransactionAsync();
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

    public static async Task<object> SendMessageToGroup(
        this NotioContext notioContext, int chatId,
        int senderId, string content)
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

    // Thêm người dùng vào nhóm
    public static async Task<object> AddUserToGroup(this NotioContext notioContext, int chatId, int userId)
    {
        bool userInChat = await notioContext.UserChats
            .AnyAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        if (userInChat)
        {
            return new { Message = "User is already in the chat" };
        }

        var userChat = new UserChat
        {
            UserId = userId,
            ChatId = chatId,
            UserRole = UserRole.Member,
            LastReadAt = DateTime.UtcNow
        };

        await notioContext.UserChats.AddAsync(userChat);
        await notioContext.SaveChangesAsync();

        return new { Message = "User added to the group successfully" };
    }

    // Xóa người dùng khỏi nhóm
    public static async Task<object> RemoveUserFromGroup(this NotioContext notioContext, int chatId, int userId)
    {
        var userChat = await notioContext.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        if (userChat == null)
        {
            return new { Message = "User is not part of the chat" };
        }

        notioContext.UserChats.Remove(userChat);
        await notioContext.SaveChangesAsync();

        return new { Message = "User removed from the group successfully" };
    }

    // Đổi tên nhóm
    public static async Task<object> RenameGroup(this NotioContext notioContext, int chatId, int userId, string newChatName)
    {
        var chat = await notioContext.Chats
            .FirstOrDefaultAsync(c => c.ChatId == chatId);

        if (chat == null)
        {
            return new { Message = "Chat not found" };
        }

        var userChat = await notioContext.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        if (userChat == null || userChat.UserRole != UserRole.Admin)
        {
            return new { Message = "Only an admin can rename the group" };
        }

        chat.ChatName = newChatName;
        await notioContext.SaveChangesAsync();

        return new { Message = "Group name updated successfully" };
    }

    // Thay đổi vai trò người dùng trong nhóm
    public static async Task<object> ChangeUserRole(this NotioContext notioContext, int chatId, int userId, UserRole newRole, int adminId)
    {
        var userChat = await notioContext.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        if (userChat == null)
        {
            return new { Message = "User is not part of the chat" };
        }

        var adminUserChat = await notioContext.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == adminId);

        if (adminUserChat == null || adminUserChat.UserRole != UserRole.Admin)
        {
            return new { Message = "Only an admin can change the user role" };
        }

        userChat.UserRole = newRole;
        await notioContext.SaveChangesAsync();

        return new { Message = "User role updated successfully" };
    }

    public static async Task<object> SendMessageWithAttachment(
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

        await notioContext.MessageAttachments.AddAsync(newAttachment);
        await notioContext.SaveChangesAsync();

        return new { Message = "Message with attachment sent successfully", newMessage.MessageId };
    }
}