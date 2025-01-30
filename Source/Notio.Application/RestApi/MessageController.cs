using Microsoft.EntityFrameworkCore;
using Notio.Database;
using Notio.Database.Entities;
using Notio.Web.Enums;
using Notio.Web.Http;
using Notio.Web.Routing;
using Notio.Web.Security;
using Notio.Web.WebApi;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class MessageController(NotioContext context) : WebApiController
{
    private readonly NotioContext _context = context;

    // DTO để nhận dữ liệu từ client
    public class MessageDto
    {
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    // Kiểm tra tính hợp lệ của token và lấy thông tin người dùng
    private async Task<User?> GetUserFromToken()
    {
        string? authorizationHeader = HttpContext.Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return null;
        }

        string token = authorizationHeader.Replace("Bearer ", "");
        try
        {
            var claims = JwtToken.DecodeToken(token);
            string? userId = claims["sub"]?.ToString();
            if (userId == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return null;
            }

            return await _context.Users.FindAsync(userId);
        }
        catch (Exception)
        {
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return null;
        }
    }

    // Lấy danh sách tin nhắn của một chat
    [Route(HttpVerbs.Get, "/messages/{chatId}")]
    public async Task<object> GetMessages(int chatId)
    {
        var user = await GetUserFromToken();
        if (user == null)
        {
            return new { message = "Unauthorized" };
        }

        var messages = await _context.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        if (messages.Count == 0)
        {
            Response.StatusCode = (int)HttpStatusCode.NoContent;
            return new { message = "No messages available" };
        }

        return new { messages };
    }

    // Gửi một tin nhắn mới
    [Route(HttpVerbs.Post, "/messages")]
    public async Task<object> SendMessage()
    {
        var user = await GetUserFromToken();
        if (user == null)
        {
            return new { message = "Unauthorized" };
        }

        MessageDto messageDto = await HttpContext.GetRequestDataAsync<MessageDto>();

        var chat = await _context.Chats.FindAsync(messageDto.ChatId);
        if (chat == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return new { message = "Chat not found" };
        }

        var sender = await _context.Users.FindAsync(messageDto.SenderId);
        if (sender == null || sender.UserId != user.UserId)
        {
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return new { message = "You are not authorized to send this message" };
        }

        var message = new Message
        {
            ChatId = messageDto.ChatId,
            SenderId = messageDto.SenderId,
            Content = messageDto.Content,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(message);
        await _context.SaveChangesAsync();

        Response.StatusCode = (int)HttpStatusCode.Created;
        return new { message = "Message sent successfully" };
    }

    // Xóa một tin nhắn
    [Route(HttpVerbs.Delete, "/messages/{messageId}")]
    public async Task<object> DeleteMessage(int messageId)
    {
        var user = await GetUserFromToken();
        if (user == null)
        {
            return new { message = "Unauthorized" };
        }

        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return new { message = "Message not found" };
        }

        // Kiểm tra xem người dùng có quyền xóa tin nhắn này không
        if (message.SenderId != user.UserId)
        {
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return new { message = "You do not have permission to delete this message" };
        }

        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();

        Response.StatusCode = (int)HttpStatusCode.NoContent;
        return new { message = "Message deleted successfully" };
    }
}